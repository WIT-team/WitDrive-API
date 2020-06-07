using System.Threading;
using System.Threading.Tasks;

namespace MDBFS.Misc
{
    public class CustomReaderWriterLock
    {
        private readonly SemaphoreSlim _r;
        private long _b;
        private  long _usrCount;

        public long UsersCounter
        {
            get
            {
                lock (this)
                {
                    return _usrCount;
                }
            }
        }

        private readonly SemaphoreSlim _g;

        public CustomReaderWriterLock()
        {
            _r = new SemaphoreSlim(1, 1);
            _b = 0;
            _usrCount = 0;
            _g = new SemaphoreSlim(1,1);
        }
        public void AcquireReaderLock()
        {
            _r.Wait();
            _b++;
            if (_b == 1) _g.Wait();
            lock (this) _usrCount++;
            _r.Release();
        }

        public void ReleaseReaderLock()
        {
            _r.Wait();
            _b--;
            if (_b == 0) _g.Release();
            lock (this) _usrCount--;
            _r.Release();
        }

        public void AcquireWriterLock()
        {
            _g.Wait();
            lock (this) _usrCount++;
        }

        public void ReleaseWriterLock()
        {
            lock (this) _usrCount--;
            _g.Release();
        }

        public async Task AcquireReaderLockAsync()
        {
            await _r.WaitAsync();
            _b++;
            if (_b == 1) await _g.WaitAsync();
            lock (this) _usrCount++;
            _r.Release();
        }

        public async Task AcquireWriterLockAsync()
        {
            await _g.WaitAsync();
            lock (this) _usrCount++;
        }

        public async Task ReleaseReaderLockAsync()
        {
            await _r.WaitAsync();
            _b--;
            if (_b == 0) _g.Release();
            lock (this) _usrCount--;
            _r.Release();
        }

    }
}