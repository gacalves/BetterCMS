﻿using System.Runtime.Serialization;

namespace BetterCms.Module.Api.Operations.Pages.GetSitemapNodes
{
    [DataContract]
    public class GetSitemapNodesResponse : ListResponseBase<NodeModel>
    {
    }
}