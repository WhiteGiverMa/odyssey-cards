namespace OdysseyCards.Map
{
    public class MapEdge
    {
        public int FromNodeId { get; set; }
        public int ToNodeId { get; set; }

        public MapEdge(int fromNodeId, int toNodeId)
        {
            FromNodeId = fromNodeId;
            ToNodeId = toNodeId;
        }

        public bool Connects(int nodeIdA, int nodeIdB)
        {
            return (FromNodeId == nodeIdA && ToNodeId == nodeIdB) ||
                   (FromNodeId == nodeIdB && ToNodeId == nodeIdA);
        }

        public bool Contains(int nodeId)
        {
            return FromNodeId == nodeId || ToNodeId == nodeId;
        }

        public int GetOtherNode(int nodeId)
        {
            if (FromNodeId == nodeId) return ToNodeId;
            if (ToNodeId == nodeId) return FromNodeId;
            return -1;
        }
    }
}
