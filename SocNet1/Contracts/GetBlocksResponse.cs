﻿namespace SocNet1.Contracts
{
    public class GetBlocksResponse
    {
        public int id { get; set; }
        public int blocker_id { get; set; }
        public int blocked_id { get; set; }
        public DateTime created_at { get; set; }
    }
}