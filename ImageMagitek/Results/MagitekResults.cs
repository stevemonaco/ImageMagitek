using System.Collections.Generic;
using OneOf;

namespace ImageMagitek
{
    public abstract class MagitekResults : OneOfBase<MagitekResults.Success, MagitekResults.Failed>
    {
        public static Success SuccessResults { get; } = new Success();

        public class Success : MagitekResults { }

        public class Failed : MagitekResults
        {
            public List<string> Reasons { get; }

            public Failed() => Reasons = new List<string>();
            public Failed(IEnumerable<string> reasons) => Reasons = new List<string>(reasons);
        }

        public bool HasSucceeded => IsT0;
        public MagitekResults.Success AsSuccess => AsT0;

        public bool HasFailed => IsT1;
        public MagitekResults.Failed AsError => AsT1;
    }

    public abstract class MagitekResults<T> : OneOfBase<MagitekResults<T>.Success, MagitekResults<T>.Failed>
    {
        public class Success : MagitekResults<T>
        {
            public T Result { get; }
            public Success(T result) => Result = result;
        }

        public class Failed : MagitekResults<T>
        {
            public List<string> Reasons { get; }

            public Failed() => Reasons = new List<string>();
            public Failed(IEnumerable<string> reasons) => Reasons = new List<string>(reasons);
            public Failed(string reason) => Reasons = new List<string> { reason };
        }

        public bool HasSucceeded => IsT0;
        public MagitekResults<T>.Success AsSuccess => AsT0;

        public bool HasFailed => IsT1;
        public MagitekResults<T>.Failed AsError => AsT1;
    }
}
