﻿namespace SocNet1.Contracts
{
    public class CreateGroupMembersRequest
    {
        public int group_id { get; set; }
        public int user_id { get; set; }
        public string role { get; set; }
        public DateTime joined_at { get; set; }
    }
}