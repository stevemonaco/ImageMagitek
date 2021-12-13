using OneOf;

namespace ImageMagitek;

public class MagitekResult : OneOfBase<MagitekResult.Success, MagitekResult.Failed>
{
    public static Success SuccessResult { get; } = new Success();

    public MagitekResult(OneOf<Success, Failed> input) : base(input) { }

    public sealed class Success { }

    public sealed class Failed
    {
        public string Reason { get; }
        public Failed(string reason) => Reason = reason;
    }

    public bool HasSucceeded => IsT0;
    public Success AsSuccess => AsT0;

    public bool HasFailed => IsT1;
    public Failed AsError => AsT1;

    public static implicit operator MagitekResult(Success input) => new MagitekResult(input);
    public static implicit operator MagitekResult(Failed input) => new MagitekResult(input);
}

public class MagitekResult<T> : OneOfBase<MagitekResult<T>.Success, MagitekResult<T>.Failed>
{
    public MagitekResult(OneOf<Success, Failed> input) : base(input) { }

    public sealed class Success
    {
        public T Result { get; }
        public Success(T result) => Result = result;
    }

    public sealed class Failed
    {
        public string Reason { get; }
        public Failed(string reason) => Reason = reason;
    }

    public bool HasSucceeded => IsT0;
    public Success AsSuccess => AsT0;

    public bool HasFailed => IsT1;
    public Failed AsError => AsT1;

    public static implicit operator MagitekResult<T>(Success input) => new MagitekResult<T>(input);
    public static implicit operator MagitekResult<T>(Failed input) => new MagitekResult<T>(input);
}
