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

    public abstract class MagitekResult<T> : OneOfBase<MagitekResult<T>.Success, MagitekResult.Failed>
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
    }

    //public enum ImageCopyOperation { None, ExactIndex, RemapByPalette, RemapByAnyIndex }
    //public abstract class ImageCopyResult<TCopyOperation> : 
    //    OneOfBase<ImageCopyResult<TImage, TCopyOperation>.Success, ImageCopyResult<TImage, TCopyOperation>.Failed>
    //{
    //    public class Success : ImageCopyResult<TImage, TCopyOperation>
    //    {
    //        public TImage Result { get; }
    //        public TCopyOperation Operation { get; }

    //        public Success(TImage result, TCopyOperation operation)
    //        {
    //            Result = result;
    //            Operation = operation;
    //        }
    //    }

    //    public class Failed : ImageCopyResult<TImage, TCopyOperation>
    //    {
    //        public string Reason { get; }
    //        public Failed(string reason) => Reason = reason;
    //    }
    //}
}
