using OneOf;

namespace ImageMagitek
{
    public abstract class MagitekResult : OneOfBase<MagitekResult.Success, MagitekResult.Failed>
    {
        public static Success SuccessResult { get; } = new Success();

        public class Success : MagitekResult { }

        public class Failed : MagitekResult
        {
            public string Reason { get; }
            public Failed(string reason) => Reason = reason;
        }

        public bool HasSucceeded => IsT0;
        public MagitekResult.Success AsSuccess => AsT0;

        public bool HasFailed => IsT1;
        public MagitekResult.Failed AsError => AsT1;
    }

    public abstract class MagitekResult<T> : OneOfBase<MagitekResult<T>.Success, MagitekResult<T>.Failed>
    {
        public class Success : MagitekResult<T>
        {
            public T Result { get; }
            public Success(T result) => Result = result;
        }

        public class Failed : MagitekResult<T>
        {
            public string Reason { get; }
            public Failed(string reason) => Reason = reason;
        }

        public bool HasSucceeded => IsT0;
        public MagitekResult<T>.Success AsSuccess => AsT0;

        public bool HasFailed => IsT1;
        public MagitekResult<T>.Failed AsError => AsT1;
    }
}
