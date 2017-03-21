using CsvHelper;
using CsvHelper.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Elite.CRM.Plugins.BordereauImport
{
    internal class ErrorFileWriter
    {
        private static readonly CsvConfiguration csvConfig = new CsvConfiguration() { };

        internal static void WriteErrors(IEnumerable<BordereauError> errors, Stream stream)
        {
            WriteErrors(errors, new StreamWriter(stream, Encoding.UTF8));
        }

        internal static void WriteErrors(IEnumerable<BordereauError> errors, TextWriter writer)
        {
            using (var csvWriter = new CsvWriter(writer, csvConfig))
            {
                // we use manual mapping, because using class-map breaks debugging using plug-in profiler in PRT
                csvWriter.WriteField<string>("Row Number");
                csvWriter.WriteField<string>("Column");
                csvWriter.WriteField<string>("Error Code");
                csvWriter.WriteField<string>("Error Description");
                csvWriter.WriteField<string>("Technical Error Details");
                csvWriter.NextRecord();

                foreach (var err in errors)
                {
                    csvWriter.WriteField<int>(err.RowNumber);
                    csvWriter.WriteField<string>(err.ColumnLabel ?? "");
                    csvWriter.WriteField<string>(err.ErrorCode ?? "");
                    csvWriter.WriteField<string>(err.ErrorDescription ?? "");
                    if(err.ErrorType != BordereauErrorType.BusinessError)
                        csvWriter.WriteField<string>(err.ErrorDetails ?? "");
                    else
                        csvWriter.WriteField<string>(err.Message ?? "");
                    csvWriter.NextRecord();
                }
            }
        }
    }

}
