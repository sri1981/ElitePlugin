using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Elite.CRM.Plugins.BordereauImport
{
    enum BordereauExceptionType
    {
        TemplateError,
        DataError,
        CrmError
    }

    class BordereauException : Exception
    {
        public BordereauExceptionType ErrorType { get; private set; }
        public BordereauError Error { get; private set; }
        
        public BordereauException(BordereauExceptionType errorType, string message, BordereauError error = null)
            : base(message)
        {
            this.ErrorType = errorType;
            this.Error = error;
        }

        public static BordereauException TemplateError(string message, BordereauError error = null)
        {
            return new BordereauException(BordereauExceptionType.TemplateError, message, error);
        }

        public static BordereauException DataError(string message, BordereauError error = null)
        {
            return new BordereauException(BordereauExceptionType.DataError, message, error);
        }

        public static BordereauException CrmError(string message, BordereauError error = null)
        {
            return new BordereauException(BordereauExceptionType.CrmError, message, error);
        }
    }
}
