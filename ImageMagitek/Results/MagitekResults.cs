using System.Collections.Generic;
using OneOf;
using System;
using System.Text;

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
        }
    }
}
