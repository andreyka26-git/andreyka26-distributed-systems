using System.Diagnostics;

namespace DistributedCache.Common.NodeManagement
{
    public class PhysicalNodeProvider : IDisposable, IPhysicalNodeProvider
    {
        private const string MasterAssemblyName = "DistributedCache.Master";
        private const string ChildNodeAssemblyName = "DistributedCache.ChildNode";
        private const string LoadBalancerNodeAssemblyName = "DistributedCache.LoadBalancer";

        public int _currentAvailablePort = 7005;

        private readonly List<PhysicalNode> _childNodes = new List<PhysicalNode>();
        private readonly List<PhysicalNode> _loadBalancerNodes = new List<PhysicalNode>();

        public IReadOnlyList<PhysicalNode> LoadBalancers => _loadBalancerNodes;
        public IReadOnlyList<PhysicalNode> ChildNodes => _childNodes;

        private readonly Dictionary<PhysicalNode, Process> _processes = new Dictionary<PhysicalNode, Process>();

        private readonly string _childExecutableAssemblyPath;
        private readonly string _loadBalancerExecutableAssemblyPath;

        public PhysicalNodeProvider()
        {
            var masterServiceExecutableLocation = Process.GetCurrentProcess().MainModule.FileName;
            _childExecutableAssemblyPath = masterServiceExecutableLocation.Replace(MasterAssemblyName, ChildNodeAssemblyName);
            _loadBalancerExecutableAssemblyPath = masterServiceExecutableLocation.Replace(MasterAssemblyName, LoadBalancerNodeAssemblyName);
        }

        public async Task<PhysicalNode> CreateChildPhysicalNodeAsync(int? port = default, CancellationToken cancellationToken = default)
        {
            var node = await CreateNewPhysicalNodeAsync(_childExecutableAssemblyPath, port, cancellationToken);
            _childNodes.Add(node);
            return node;
        }

        public async Task<PhysicalNode> CreateLoadBalancerPhysicalNodeAsync(int? port = default, CancellationToken cancellationToken = default)
        {
            var node = await CreateNewPhysicalNodeAsync(_loadBalancerExecutableAssemblyPath, port, cancellationToken);
            _loadBalancerNodes.Add(node);
            return node;
        }

        public async Task<PhysicalNode> CreateNewPhysicalNodeAsync(string assemblyPath, int? port = default, CancellationToken cancellationToken = default)
        {
            if (!port.HasValue)
            {
                port = ++_currentAvailablePort;
            }
            else
            {
                if (_currentAvailablePort > port)
                {
                    throw new ArgumentException($"Port should be monotonically increasing, set something above {_currentAvailablePort}");
                }

                _currentAvailablePort = port.Value + 1;
            }

            var url = $"https://localhost:{port}";

            var node = new PhysicalNode(new Uri(url));

            if (_processes.ContainsKey(node))
            {
                throw new ArgumentException($"this port is occupied already");
            }

            var args = $"--urls={url}";

            var process = new Process();
            process.StartInfo.FileName = assemblyPath;
            process.StartInfo.Arguments = args;

            process.Start();

            await Task.Delay(2 * 1000);

            _processes.Add(node, process);

            return node;
        }

        public void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposing)
            {
                return;
            }

            foreach(var (node, process) in _processes)
            {
                process.Kill();
            }
        }
    }
}
