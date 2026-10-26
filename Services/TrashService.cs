using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.VisualBasic.FileIO;

namespace TrashAll.Services
{
    public class TrashService
    {
        public async Task<(bool success, string? errorMessage)> MoveToRecycleBinAsync(string filePath)
        {
            return await Task.Run(() =>
            {
                if (!File.Exists(filePath))
                {
                    return (false, (string?)"O arquivo não foi encontrado ou já foi movido.");
                }

                try
                {
                    FileSystem.DeleteFile(
                        filePath,
                        UIOption.OnlyErrorDialogs,
                        RecycleOption.SendToRecycleBin
                    );
                    return (true, (string?)null);
                }
                catch (Exception ex)
                {
                    return (false, (string?)ex.Message);
                }
            });
        }

        public async Task<(bool success, string? errorMessage)> RestoreFromRecycleBinAsync(string originalFilePath)
        {
            return await Task.Run(() =>
            {
                try
                {
                    if (File.Exists(originalFilePath))
                    {
                        return (true, (string?)null);
                    }

                    string fileName = Path.GetFileName(originalFilePath);
                    string? dirName = Path.GetDirectoryName(originalFilePath);

                    Type? shellType = Type.GetTypeFromProgID("Shell.Application");
                    if (shellType == null)
                    {
                        return (false, "Shell.Application COM indisponível.");
                    }

                    dynamic? shell = Activator.CreateInstance(shellType);
                    if (shell == null)
                    {
                        return (false, "Não foi possível criar instância do Windows Shell.");
                    }

                    dynamic? recycleBin = shell.NameSpace(10);
                    if (recycleBin == null)
                    {
                        return (false, "Não foi possível abrir a Lixeira do Windows.");
                    }

                    foreach (var item in recycleBin.Items())
                    {
                        string name = (string)item.Name;
                        if (string.Equals(name, fileName, StringComparison.OrdinalIgnoreCase))
                        {
                            if (!string.IsNullOrEmpty(dirName))
                            {
                                string origLoc = (string)recycleBin.GetDetailsOf(item, 1);
                                if (!string.IsNullOrEmpty(origLoc) && 
                                    !string.Equals(origLoc.Trim(), dirName.Trim(), StringComparison.OrdinalIgnoreCase))
                                {
                                    continue;
                                }
                            }

                            foreach (var verb in item.Verbs())
                            {
                                string verbName = (string)verb.Name;
                                string clean = verbName.Replace("&", "").ToLowerInvariant();
                                if (clean.Contains("restaur") || clean.Contains("restore") || clean.Contains("undelete"))
                                {
                                    verb.DoIt();

                                    for (int i = 0; i < 20; i++)
                                    {
                                        if (File.Exists(originalFilePath))
                                            return (true, null);
                                        System.Threading.Thread.Sleep(50);
                                    }

                                    return (true, null);
                                }
                            }
                        }
                    }

                    return (false, "Item não localizado na Lixeira.");
                }
                catch (Exception ex)
                {
                    return (false, ex.Message);
                }
            });
        }
    }
}
