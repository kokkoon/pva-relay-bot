// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Rest.Serialization;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace Microsoft.PowerVirtualAgents.Samples.BotConnectorApp
{
    /// <summary>
    /// Bot Service class to interact with bot
    /// </summary>
    public class TokenStore
    {
        public string Token { get; set; }

    }
}
