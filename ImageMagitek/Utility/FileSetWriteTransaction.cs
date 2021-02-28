using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace ImageMagitek.Utility
{
    //public interface IFileSetWriteTransaction
    //{
    //    MagitekResults Transact(IEnumerable<IFileWriteTransaction> fileActions);
    //}

    //public class FileSetWriteTransaction : IFileSetWriteTransaction
    //{
    //    public MagitekResults Transact(IEnumerable<IFileWriteTransaction> fileActions)
    //    {
    //        TryStartSave(fileActions);

    //        return MagitekResults.SuccessResults;
    //    }

    //    private static MagitekResult TryStartSave(IEnumerable<IFileWriteTransaction> fileActions)
    //    {
    //        string destName = string.Empty;
    //        try
    //        {
    //            foreach (var action in fileActions.Where(x => File.Exists(x.FileName)))
    //            {
    //                destName = Path.ChangeExtension(action.FileName, ".bak");
    //                File.Move(action.FileName, destName, true);
    //            }

    //            return MagitekResult.SuccessResult;
    //        }
    //        catch (Exception ex)
    //        {
    //            return new MagitekResult.Failed($"Failed to rename existing XML files to .bak ('{destName}') in preparation for save: {ex.Message}");
    //        }
    //    }

    //    private static MagitekResults TryRevertSave(IEnumerable<IFileWriteTransaction> fileActions)
    //    {
    //        var errors = new List<string>();

    //        foreach (var action in fileActions)
    //        {
    //            action.Revert();
    //        }

    //        if (errors.Count > 0)
    //            return new MagitekResults.Failed(errors);
    //        else
    //            return MagitekResults.SuccessResults;
    //    }
    //}
}
