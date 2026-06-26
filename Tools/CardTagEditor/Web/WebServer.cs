using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using OdysseyCards.Tools.CardTagEditor.Schema;
using OdysseyCards.Tools.CardTagEditor.Services;

namespace OdysseyCards.Tools.CardTagEditor.Web;

/// <summary>
/// Web UI 服务器——HttpListener + JSON API + 内嵌 Alpine.js SPA。
/// 路由：
///   GET  /                  → 静态 SPA 页面
///   GET  /api/cards         → 所有卡牌摘要
///   GET  /api/card/:id      → 单卡完整数据
///   POST /api/card/:id      → 保存单卡 { MechanicTags:int, Keywords:int[] }
///   GET  /api/themes        → 所有主题摘要
///   GET  /api/theme/:hero   → 单主题完整数据
///   POST /api/theme/:hero   → 保存主题 { TagWeights, KeywordWeights, CoreCardIds }
///   POST /api/migrate       → 执行迁移 { DryRun:bool }
///   GET  /api/validate      → 校验报告
///   GET  /api/schema        → 枚举位/关键词定义（供前端渲染 checkbox）
/// </summary>
public class WebServer : IDisposable
{
	private readonly HttpListener _listener;
	private readonly CardTagService _svc;
	private readonly CancellationTokenSource _cts = new();

	private static readonly JsonSerializerOptions JsonOpts = new()
	{
		WriteIndented = false,
		PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
		Encoder = System.Text.Encodings.Web.JavaScriptEncoder.Create(System.Text.Unicode.UnicodeRanges.All),
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
	};

	public WebServer(CardTagService svc, string prefix = "http://localhost:8765/")
	{
		_svc = svc;
		_listener = new HttpListener();
		_listener.Prefixes.Add(prefix);
	}

	/// <summary>启动服务器（阻塞直到取消）。</summary>
	public async Task StartAsync(CancellationToken token = default)
	{
		_listener.Start();

		try
		{
			while (!token.IsCancellationRequested && !_cts.IsCancellationRequested)
			{
				var ctx = await _listener.GetContextAsync().WaitAsync(token);
				_ = Task.Run(() => HandleRequestAsync(ctx), token);
			}
		}
		catch (OperationCanceledException) { }
		catch (HttpListenerException) when (_cts.IsCancellationRequested) { }
		finally
		{
			_listener.Stop();
		}
	}

	public void Stop() => _cts.Cancel();

	private async Task HandleRequestAsync(HttpListenerContext ctx)
	{
		var req = ctx.Request;
		var res = ctx.Response;
		try
		{
			var path = req.Url?.AbsolutePath.Trim('/') ?? "";
			var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);

			// 路由分发
			switch (segments.Length == 0 ? "/" : req.HttpMethod)
			{
				case var _ when segments.Length == 0:
					await SendStatic(res, "index.html");
					return;
				case "GET" when path == "api/cards":
					await SendJson(res, _svc.ListCards());
					return;
				case "GET" when segments is ["api", "card", var id]:
					await SendJson(res, BuildCardDto(id));
					return;
				case "POST" when segments is ["api", "card", var id]:
					await SaveCard(res, id, req);
					return;
				case "GET" when path == "api/themes":
					await SendJson(res, BuildThemesList());
					return;
				case "GET" when segments is ["api", "theme", var hero]:
					await SendJson(res, BuildThemeDto(hero));
					return;
				case "POST" when segments is ["api", "theme", var hero]:
					await SaveTheme(res, hero, req);
					return;
				case "POST" when path == "api/migrate":
					await Migrate(res, req);
					return;
				case "GET" when path == "api/validate":
					await SendJson(res, _svc.Validate());
				 return;
				case "GET" when path == "api/schema":
					await SendJson(res, BuildSchemaDto());
					return;
				default:
					await SendJson(res, 404, new { error = "Not Found", path });
					return;
			}
		}
		catch (Exception ex)
		{
			Console.Error.WriteLine($"[WebServer] 请求处理错误: {ex.Message}");
			res.StatusCode = 500;
			res.ContentType = "application/json; charset=utf-8";
			await res.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(
				JsonSerializer.Serialize(new { error = ex.Message }, JsonOpts)));
		}
		finally
		{
			res.Close();
		}
	}

	// ===== DTO 构造 =====

	private object BuildCardDto(string id)
	{
		var card = _svc.DumpCard(id) ?? throw new FileNotFoundException($"卡牌 '{id}' 不存在");
		return new
		{
			Id = card.Id,
			CardName = card.CardName,
			Type = card.Type,
			MechanicTags = card.MechanicTags,
			MechanicTagNames = card.GetMechanicTagNames(),
			Keywords = card.Keywords,
			KeywordNames = card.GetKeywordNames(),
		};
	}

	private object BuildThemesList()
	{
		return _svc.ListThemes().Select(t => new
		{
			HeroId = t.HeroId,
			ThemeName = t.ThemeName,
			TagWeightsCount = t.TagWeights.Count,
			KeywordWeightsCount = t.HasKeywordWeights ? t.KeywordWeights.Count : 0,
			CoreCardIdsCount = t.CoreCardIds.Length,
		}).ToList();
	}

	private object BuildThemeDto(string hero)
	{
		var t = _svc.DumpTheme(hero) ?? throw new FileNotFoundException($"主题 '{hero}' 不存在");
		return new
		{
			HeroId = t.HeroId,
			ThemeName = t.ThemeName,
			TagWeights = t.TagWeights,
			KeywordWeights = t.HasKeywordWeights ? t.KeywordWeights : new Dictionary<int, int>(),
			CoreCardIds = t.CoreCardIds,
		};
	}

	private static object BuildSchemaDto()
	{
		return new
		{
			MechanicTags = CardMechanicTagValues.AllBits
				.Select(b => new { Bit = b, Name = CardMechanicTagValues.BitToName[b] })
				.ToList(),
			Keywords = KeywordValues.AllValues
				.Select(v => new { Value = v, Name = KeywordValues.ValueToName[v] })
				.ToList(),
		};
	}

	// ===== 写操作 =====

	private async Task SaveCard(HttpListenerResponse res, string id, HttpListenerRequest req)
	{
		var body = await ReadJsonAsync<SaveCardBody>(req);
		_svc.SaveCard(id, body.MechanicTags, body.Keywords ?? Array.Empty<int>());
		await SendJson(res, new { ok = true, id });
	}

	private async Task SaveTheme(HttpListenerResponse res, string hero, HttpListenerRequest req)
	{
		var body = await ReadJsonAsync<SaveThemeBody>(req);
		_svc.SaveTheme(hero, body.TagWeights ?? new(), body.KeywordWeights, body.CoreCardIds ?? Array.Empty<string>(), null);
		await SendJson(res, new { ok = true, hero });
	}

	private async Task Migrate(HttpListenerResponse res, HttpListenerRequest req)
	{
		var body = await ReadJsonAsync<MigrateBody>(req);
		var result = _svc.Migrate(body?.DryRun ?? false);
		await SendJson(res, new { result.ChangeCount, result.DryRun, Changes = result.Changes });
	}

	// ===== 辅助 =====

	private static async Task<T?> ReadJsonAsync<T>(HttpListenerRequest req) where T : class
	{
		using var sr = new StreamReader(req.InputStream, req.ContentEncoding ?? Encoding.UTF8);
		var text = await sr.ReadToEndAsync();
		return string.IsNullOrWhiteSpace(text) ? null : JsonSerializer.Deserialize<T>(text, JsonOpts);
	}

	private static async Task SendJson(HttpListenerResponse res, object data)
	{
		res.StatusCode = 200;
		res.ContentType = "application/json; charset=utf-8";
		var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(data, JsonOpts));
		await res.OutputStream.WriteAsync(bytes);
	}

	private static async Task SendJson(HttpListenerResponse res, int status, object data)
	{
		res.StatusCode = status;
		res.ContentType = "application/json; charset=utf-8";
		var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(data, JsonOpts));
		await res.OutputStream.WriteAsync(bytes);
	}

	private static async Task SendStatic(HttpListenerResponse res, string fileName)
	{
		var content = fileName == "index.html" ? IndexPage.Html : "<!doctype html><title>404</title>Not Found";
		res.StatusCode = 200;
		res.ContentType = "text/html; charset=utf-8";
		var bytes = Encoding.UTF8.GetBytes(content);
		await res.OutputStream.WriteAsync(bytes);
	}

	public void Dispose()
	{
		_cts.Cancel();
		_cts.Dispose();
		(_listener as IDisposable)?.Dispose();
	}

	// ===== 请求体类型 =====

	private sealed class SaveCardBody
	{
		public int MechanicTags { get; set; }
		public int[]? Keywords { get; set; }
	}

	private sealed class SaveThemeBody
	{
		public Dictionary<int, int>? TagWeights { get; set; }
		public Dictionary<int, int>? KeywordWeights { get; set; }
		public string[]? CoreCardIds { get; set; }
	}

	private sealed class MigrateBody
	{
		public bool DryRun { get; set; }
	}
}
