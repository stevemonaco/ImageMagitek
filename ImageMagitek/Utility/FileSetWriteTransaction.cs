using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace ImageMagitek.Utility
{
    public interface IFileChangeTransactionRunner
    {
        MagitekResults Transact();
    }

    public class FileSetWriteTransaction : IFileChangeTransactionRunner
    {
        private readonly IList<IFileChangeTransaction> _actions;

        public FileSetWriteTransaction(IEnumerable<IFileChangeTransaction> actions)
        {
            _actions = actions.ToList();
        }

        public MagitekResults Transact()
        {
            return TryRunTransactionSet().Match<MagitekResults>(
                success => MagitekResults.SuccessResults,
                failed =>
                {
                    var compositeErrors = new List<string>
                    {
                        failed.Reason
                    };

                    return TrySetRollback().Match(
                        rollBackSuccess =>
                        {
                            return new MagitekResults.Failed(compositeErrors);
                        },
                        rollbackFailure =>
                        {
                            compositeErrors.AddRange(rollbackFailure.Reasons);
                            return new MagitekResults.Failed(compositeErrors);
                        });
                });
        }

        private MagitekResult TryRunTransactionSet()
        {
            string destName = string.Empty;
            try
            {
                foreach (var action in _actions)
                {
                    action.Prepare();
                }

                foreach (var action in _actions)
                {
                    action.Execute();
                }

                foreach (var action in _actions)
                {
                    action.Complete();
                }

                return MagitekResult.SuccessResult;
            }
            catch (Exception ex)
            {
                return new MagitekResult.Failed($"Failed to rename existing XML files to .bak ('{destName}') in preparation for save: {ex.Message}");
            }
        }

        private MagitekResults TrySetRollback()
        {
            var errors = new List<string>();

            foreach (var action in _actions.Where(x => x.State == TransactionState.RollbackRequired))
            {
                if (action.Rollback())
                    errors.Add($"'{action.PrimaryFileName}' failed to rollback");
            }

            if (errors.Count > 0)
                return new MagitekResults.Failed(errors);
            else
                return MagitekResults.SuccessResults;
        }
    }
}
