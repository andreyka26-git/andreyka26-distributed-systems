using DistributedCache.Common;
using DistributedCache.Common.Clients;
using DistributedCache.Common.Hashing;
using DistributedCache.Common.NodeManagement;
using Microsoft.AspNetCore.Mvc;

namespace DistributedCache.Master.Controllers
{
    [ApiController]
    [Route("master")]
    public class MasterController : ControllerBase
    {
        private readonly INodeManager _nodeManager;
        private readonly IHashService _hashService;
        private readonly IHashingRing _hashingRing;
        private readonly IChildNodeClient _childNodeService;

        public MasterController(
            INodeManager nodeManager,
            IHashService hashService,
            IHashingRing hashingRing,
            IChildNodeClient childNodeService)
        {
            _nodeManager = nodeManager;
            _hashService = hashService;
            _hashingRing = hashingRing;
            _childNodeService = childNodeService;
        }


    }
}