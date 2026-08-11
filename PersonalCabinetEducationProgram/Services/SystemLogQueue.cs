using System.Threading.Channels;
using PersonalCabinetEducationProgram.Models;

namespace PersonalCabinetEducationProgram.Services
{
    public sealed class SystemLogQueue
    {
        private readonly Channel<SystemRequestLog> _requests = Channel.CreateBounded<SystemRequestLog>(
            new BoundedChannelOptions(10_000)
            {
                SingleReader = true,
                SingleWriter = false,
                FullMode = BoundedChannelFullMode.Wait
            });

        private readonly Channel<SecurityEventLog> _securityEvents = Channel.CreateBounded<SecurityEventLog>(
            new BoundedChannelOptions(2_000)
            {
                SingleReader = true,
                SingleWriter = false,
                FullMode = BoundedChannelFullMode.Wait
            });

        private long _droppedRequestCount;
        private long _droppedSecurityEventCount;

        public ChannelReader<SystemRequestLog> Requests => _requests.Reader;
        public ChannelReader<SecurityEventLog> SecurityEvents => _securityEvents.Reader;
        public long DroppedRequestCount => Interlocked.Read(ref _droppedRequestCount);
        public long DroppedSecurityEventCount => Interlocked.Read(ref _droppedSecurityEventCount);

        public bool TryQueue(SystemRequestLog entry)
        {
            if (_requests.Writer.TryWrite(entry))
                return true;

            Interlocked.Increment(ref _droppedRequestCount);
            return false;
        }

        public bool TryQueue(SecurityEventLog entry)
        {
            if (_securityEvents.Writer.TryWrite(entry))
                return true;

            Interlocked.Increment(ref _droppedSecurityEventCount);
            return false;
        }
    }
}
