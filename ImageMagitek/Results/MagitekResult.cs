using OneOf;

namespace ImageMagitek;

public sealed class MagitekResult : OneOfBase<MagitekResult.Success, MagitekResult.Failed>
{
    public static Success SuccessResult { get; } = new Success();

    public MagitekResult(OneOf<Success, Failed> input) : base(input) { }

    public sealed record Success();
    public sealed record Failed(string Reason);

    public bool HasSucceeded => IsT0;
    public Success AsSuccess => AsT0;

    public bool HasFailed => IsT1;
    public Failed AsError => AsT1;

    public static implicit operator MagitekResult(Success input) => new(input);
    public static implicit operator MagitekResult(Failed input) => new(input);
}

public sealed class MagitekResult<T> : OneOfBase<MagitekResult<T>.Success, MagitekResult<T>.Failed>
{
    public MagitekResult(OneOf<Success, Failed> input) : base(input) { }

    public sealed record Success(T Result);
    public sealed record Failed(string Reason);

    public bool HasSucceeded => IsT0;
    public Success AsSuccess => AsT0;

    public bool HasFailed => IsT1;
    public Failed AsError => AsT1;

    public static implicit operator MagitekResult<T>(Success input) => new(input);
    public static implicit operator MagitekResult<T>(Failed input) => new(input);
}
