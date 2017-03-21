using Elite.CRM.Plugins.Entities;
using Elite.CRM.Plugins.ErrorHandling;
using Microsoft.Xrm.Sdk;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Elite.CRM.Plugins.BordereauImport
{
    /// <summary>
    /// Utility class encapsulating:
    ///   Import row - collection of string keys/values 
    ///   Template - collection of column mappings, containing entity, field, and type information
    /// </summary>
    class MappedRow
    {
        private BordereauImportRow _row;
        private BordereauTemplate _template;

        private IEnumerable<MappedAttribute> _attributes;
        public IEnumerable<MappedAttribute> Attributes
        {
            get
            {
                if (_attributes != null)
                    return _attributes;

                _attributes = _template.TemplateColumns.Select(col => 
                {
                    string value;
                    if (col.ValueType == null || col.ValueType == ColumnValueType.ColumnMapping)
                        value = _row[col.ColumnNumber];
                    else
                        value = col.DefaultValue;

                    return new MappedAttribute(col, value);
                });

                return _attributes;
            }
        }

        public BordereauTemplate Template
        {
            get
            {
                return _template;
            }
        }

        public int? RowNumber
        {
            get { return _row.RowNumber; }
        }

        /// <summary>
        /// Creates new Mapped Row instance. 
        /// </summary>
        /// <param name="row">Bordereau import row, which provides string data values.</param>
        /// <param name="template">Bordereau template, which is used for processing rows.</param>
        public MappedRow(BordereauImportRow row, BordereauTemplate template)
        {
            _row = row;
            _template = template;
        }
    }

}
