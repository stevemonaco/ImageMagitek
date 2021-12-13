using System.Collections.Generic;
using OneOf;

namespace ImageMagitek;

public class MagitekResults : OneOfBase<MagitekResults.Success, MagitekResults.Failed>
{
    public static Success SuccessResults { get; } = new Success();

    public MagitekResults(OneOf<Success, Failed> input) : base(input) { }

    public sealed class Success { }

    public sealed class Failed
    {
        public List<string> Reasons { get; }

        public Failed() => Reasons = new List<string>();
        public Failed(IEnumerable<string> reasons) => Reasons = new List<string>(reasons);
    }

    public bool HasSucceeded => IsT0;
    public Success AsSuccess => AsT0;

    public bool HasFailed => IsT1;
    public Failed AsError => AsT1;

    public static implicit operator MagitekResults(Success input) => new MagitekResults(input);
    public static implicit operator MagitekResults(Failed input) => new MagitekResults(input);
}

public class MagitekResults<T> : OneOfBase<MagitekResults<T>.Success, MagitekResults<T>.Failed>
{
    public MagitekResults(OneOf<Success, Failed> input) : base(input) { }

    public sealed class Success
    {
        public T Result { get; }
        public Success(T result) => Result = result;
    }

    public sealed class Failed
    {
        public List<string> Reasons { get; }

        public Failed() => Reasons = new List<string>();
        public Failed(IEnumerable<string> reasons) => Reasons = new List<string>(reasons);
        public Failed(string reason) => Reasons = new List<string> { reason };
    }

    public bool HasSucceeded => IsT0;
    public Success AsSuccess => AsT0;

    public bool HasFailed => IsT1;
    public Failed AsError => AsT1;

    public static implicit operator MagitekResults<T>(Success input) => new MagitekResults<T>(input);
    public static implicit operator MagitekResults<T>(Failed input) => new MagitekResults<T>(input);
}
