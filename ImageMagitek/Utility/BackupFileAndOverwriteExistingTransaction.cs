using System;
using System.IO;

namespace ImageMagitek.Utility
{
    public class BackupFileAndOverwriteExistingTransaction : ITransactionCommand
    {
        public string FileName { get; }
        public string Contents { get; }
        public string BackupFileName { get; }

        public Exception LastException { get; private set; }

        public BackupFileAndOverwriteExistingTransaction(string fileName, string contents)
        {
            FileName = fileName;
            Contents = contents;

            BackupFileName = Path.ChangeExtension(FileName, ".bak");
        }

        /// <summary>
        /// Prepares for writing by moving the existing file to a backup location
        /// </summary>
        /// <returns>True if successful, false if an exception occurred</returns>
        public bool Prepare()
        {
            try
            {
                if (File.Exists(FileName))
                {
                    File.Move(FileName, BackupFileName, true);
                }

                return true;
            }
            catch (Exception ex)
            {
                LastException = ex;
                return false;
            }
        }

        /// <summary>
        /// Writes the contents to a new file
        /// </summary>
        /// <returns></returns>
        public bool Execute()
        {
            try
            {
                File.WriteAllText(FileName, Contents);
                return true;
            }
            catch (Exception ex)
            {
                LastException = ex;
                return false;
            }
        }

        /// <summary>
        /// Rollbacks the operation by restoring from the backup file
        /// </summary>
        /// <returns></returns>
        public bool Rollback()
        {
            try
            {
                if (File.Exists(BackupFileName))
                {
                    File.Move(BackupFileName, FileName, true);
                }
                else
                    return false;

                return true;
            }
            catch (Exception ex)
            {
                LastException = ex;
                return false;
            }
        }

        /// <summary>
        /// Completes the transaction by removing the backup file
        /// </summary>
        /// <returns></returns>
        public bool Complete()
        {
            try
            {
                File.Delete(BackupFileName);
                return true;
            }
            catch (Exception ex)
            {
                LastException = ex;
                return false;
            }
        }
    }
}
