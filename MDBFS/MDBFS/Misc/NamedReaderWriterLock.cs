using System.Collections.Generic;
using System.Threading.Tasks;

namespace MDBFS.Misc
{
    public class NamedReaderWriterLock
    {
        private readonly Dictionary<string, CustomReaderWriterLock> _rwls;

        public NamedReaderWriterLock()
        {
            _rwls = new Dictionary<string, CustomReaderWriterLock>();
        }

        public void AcquireReaderLock(string id)
        {
            CustomReaderWriterLock crwl;
            lock (this)
            {
                if (_rwls.ContainsKey(id))
                {
                    crwl = _rwls[id];
                }
                else
                {
                    crwl = new CustomReaderWriterLock();
                    _rwls[id] = crwl;
                }
            }

            crwl.AcquireReaderLock();
        }

        public void AcquireWriterLock(string id)
        {
            CustomReaderWriterLock crwl;
            lock (this)
            {
                if (_rwls.ContainsKey(id))
                {
                    crwl = _rwls[id];
                }
                else
                {
                    crwl = new CustomReaderWriterLock();
                    _rwls[id] = crwl;
                }
            }

            crwl.AcquireWriterLock();
        }

        public void ReleaseReaderLock(string id)
        {
            CustomReaderWriterLock crwl;
            lock (this)
            {
                crwl = _rwls[id];
                if (crwl.UsersCounter - 1 <= 0) _rwls.Remove(id);
            }

            crwl.ReleaseReaderLock();
        }

        public void ReleaseWriterLock(string id)
        {
            CustomReaderWriterLock crwl;
            lock (this)
            {
                crwl = _rwls[id];
                if (crwl.UsersCounter - 1 <= 0) _rwls.Remove(id);
            }

            crwl.ReleaseWriterLock();
        }
        public async Task AcquireReaderLockAsync(string id)
        {
            CustomReaderWriterLock crwl;
            lock (this)
            {
                if (_rwls.ContainsKey(id))
                {
                    crwl = _rwls[id];
                }
                else
                {
                    crwl = new CustomReaderWriterLock();
                    _rwls[id] = crwl;
                }
            }

            await crwl.AcquireReaderLockAsync();
        }

        public async Task AcquireWriterLockAsync(string id)
        {
            CustomReaderWriterLock crwl;
            lock (this)
            {
                if (_rwls.ContainsKey(id))
                {
                    crwl = _rwls[id];
                }
                else
                {
                    crwl = new CustomReaderWriterLock();
                    _rwls[id] = crwl;
                }
            }

            await crwl.AcquireWriterLockAsync();
        }

        public async Task ReleaseReaderLockAsync(string id)
        {
            CustomReaderWriterLock crwl;
            lock (this)
            {
                crwl = _rwls[id];
                if (crwl.UsersCounter - 1 <= 0) _rwls.Remove(id);
            }

            await crwl.ReleaseReaderLockAsync();
        }

    }
}