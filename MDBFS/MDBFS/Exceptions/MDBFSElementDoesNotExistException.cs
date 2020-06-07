using System;

namespace MDBFS.Exceptions
{
    [Serializable]
    public class MdbfsElementDoesNotExistException : Exception
    {
        public MdbfsElementDoesNotExistException() { }
        public MdbfsElementDoesNotExistException(string message) : base(message) { }
        public MdbfsElementDoesNotExistException(string message, Exception inner) : base(message, inner) { }
        protected MdbfsElementDoesNotExistException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }
}
