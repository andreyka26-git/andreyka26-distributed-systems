namespace DistributedCache.Common.NodeManagement
{
    // we consider specific ring position of the virtual node as unique identifier
    // meaning no 2 virtupal nodes can point to exactly same ring position (radian or degree)
    public record VirtualNode(uint RingPosition);
}
