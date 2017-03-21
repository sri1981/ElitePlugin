using Elite.CRM.Plugins.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Elite.CRM.Plugins.BordereauImport
{
    public enum BordereauErrorType
    {
        MissingValue = 100000000,
        IncorrectFormat = 100000001,
        BusinessError = 100000002,
    }

    public class BordereauError
    {
        /// <summary>
        /// Policy or Claim number to which is this error related
        /// </summary>
        public string RecordIdentifier { get; set; }
        public int RowNumber { get; set; }

        // set from error code entity
        public string ErrorCode { get; set; }
        public string ErrorDescription { get; set; }

        public BordereauTemplateColumn Column { get; private set; }

        public string _columnLabel;
        public string ColumnLabel
        {
            get
            {
                if (!string.IsNullOrEmpty(_columnLabel))
                    return _columnLabel;
                else if (Column != null)
                    return Column.ColumnLabel;
                return null;
            }
            private set
            {
                _columnLabel = value;
            }
        }

        public BordereauErrorType ErrorType { get; private set; }
        public string Value { get; private set; }
        public string Message { get; private set; }

        public string ErrorDetails
        {
            get
            {
                return this.ToString();
            }
        }

        /// <summary>
        /// Creates an instance of Bordereau error, which is related to a value in a specific column. Use
        /// this constructor in case BordereauTemplateColumn object is available.
        /// </summary>
        /// <param name="column">Column of Bordereau, which caused the error.</param>
        /// <param name="errorType">Type of error. </param>
        /// <param name="value">Value of cell, which caused the error.</param>
        /// <param name="message">Optional message, a description of the error.</param>
        public BordereauError(BordereauTemplateColumn column, BordereauErrorType errorType, string value, string message = null)
        {
            Column = column;
            ErrorType = errorType;
            Value = value;
            Message = message;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="errorType"></param>
        /// <param name="message"></param>
        /// <param name="label"></param>
        public BordereauError(BordereauErrorType errorType, string message, string label)
        {
            ErrorType = errorType;
            Message = message;
            if (label != null)
                ColumnLabel = label;
        }

        public BordereauError(BordereauErrorType errorType, string message)
        {
            ErrorType = errorType;
            Message = message;

        }

        public override string ToString()
        {
            if (Column != null)
                return "{0}: Value '{1}' for field {2}.{3} ({4})".FormatWith(ErrorType, Value, Column.EntityName, Column.AttributeName, Column.MappingDisplayName);
            else
                return "{0}: {1}".FormatWith(ErrorType, Message);
        }
    }

    class ErrorCollection
    {
        private Dictionary<int, IList<BordereauError>> _errors = new Dictionary<int, IList<BordereauError>>();
        private int _rowNo;
        public int RowNumber { get { return _rowNo; } }

        public ErrorCollection(int initialRow)
        {
            _rowNo = initialRow;
        }

        public void AddError(BordereauError err)
        {
            if (err == null)
                return;

            if (!_errors.ContainsKey(_rowNo))
                _errors[_rowNo] = new List<BordereauError>();

            err.RowNumber = _rowNo;
            _errors[_rowNo].Add(err);
        }

        public void AddErrors(IEnumerable<BordereauError> errors)
        {
            foreach (var err in errors)
                AddError(err);
        }

        public void NextRow()
        {
            _rowNo += 1;
        }

        public bool AnyErrorsForCurrentRow()
        {
            return _errors.ContainsKey(_rowNo) && _errors[_rowNo].Any();
        }

        public IEnumerable<BordereauError> CurrentRowErrors
        {
            get
            {
                if (!_errors.ContainsKey(_rowNo))
                    return Enumerable.Empty<BordereauError>();

                return _errors[_rowNo].AsEnumerable();
            }
        }

        public IEnumerable<BordereauError> AllErrors
        {
            get
            {
                return _errors.SelectMany(errList => errList.Value);
            }
        }
    }
}
