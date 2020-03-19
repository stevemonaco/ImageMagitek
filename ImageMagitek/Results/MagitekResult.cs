using OneOf;

namespace ImageMagitek
{
    public abstract class MagitekResult : OneOfBase<MagitekResult.Success, MagitekResult.Failed>
    {
        public class Success : MagitekResult { }
        public class Failed : MagitekResult
        {
            public string Reason { get; }
            public Failed(string reason) => Reason = reason;
        }
    }

    public abstract class MagitekResult<T> : OneOfBase<MagitekResult.Success, MagitekResult.Failed>
    {
        public class Success : MagitekResult<T>
        {
            public T Result { get; }
            public Success(T result) => Result = result;
        }

        public class Failed : MagitekResult
        {
            public string Reason { get; }
            public Failed(string reason) => Reason = reason;
        }
    }
}
