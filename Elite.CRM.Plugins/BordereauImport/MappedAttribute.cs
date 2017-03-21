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
    class MappedAttribute
    {
        private static readonly CultureInfo ParsingCulture = CultureInfo.GetCultureInfo("en-GB");

        public BordereauTemplateColumn TemplateColumn { get; private set; }
        public string Value { get; private set; }

        public MappedAttribute(BordereauTemplateColumn templateCol, string value)
        {
            //ThrowIf.Argument.IsNull(templateCol, "templateCol");

            this.Value = value;
            if (!string.IsNullOrEmpty(this.Value))
                this.Value = this.Value.Trim();

            this.TemplateColumn = templateCol;
        }

        public FieldMappingFormat Format
        {
            get { return TemplateColumn.Format; }
        }

        public string EntityName
        {
            get { return TemplateColumn.EntityName; }
        }

        public string AttributeName
        {
            get { return TemplateColumn.AttributeName; }
        }

        public bool HasValue
        {
            get { return !string.IsNullOrEmpty(this.Value); }
        }

        public object ConvertedValue
        {
            get
            {
                switch (Format)
                {
                    case FieldMappingFormat.SingleLineOfText:
                    case FieldMappingFormat.Email:
                    case FieldMappingFormat.URL:
                    case FieldMappingFormat.MultipleLinesOfText:
                        return AsString();
                    case FieldMappingFormat.OptionSet:
                        return AsOptionSet();
                    case FieldMappingFormat.TwoOptions:
                        return AsBool();
                    case FieldMappingFormat.WholeNumber:
                        return AsInteger();
                    case FieldMappingFormat.DecimalNumber:
                        return AsDecimal();
                    case FieldMappingFormat.Currency:
                        var decimalVal = AsDecimal();
                        if (decimalVal == null || decimalVal == 0)
                            return null;
                        return new Money(decimalVal.Value); //!= null ? new Money(decimalVal.Value) : null;
                    case FieldMappingFormat.Date:
                        return AsDateTime();
                    case FieldMappingFormat.Lookup:
                        var entityVal = AsEntity();
                        return entityVal != null ? entityVal.ToEntityReference() : null;
                    default:
                        throw new NotImplementedException();
                }
            }
        }

        public BordereauError ValidateValue()
        {
            // missing value
            if (this.TemplateColumn.Mandatory && !this.HasValue)
                return Error(BordereauErrorType.MissingValue);

            //if(this.TemplateColumn.Mandatory && this.HasValue && this.Format == FieldMappingFormat.Currency)
            //{
            //    if(this.ConvertedValue == 0)
            //        return Error(BordereauErrorType.MissingValue);
            //}


            // lookups are not validated by attribute itself
            if (this.TemplateColumn.Format == FieldMappingFormat.Lookup)
                return null;

            object converted = null;

            // try typed conversion, except lookup which always pass
            try
            {
                converted = this.ConvertedValue; 
            }
            catch (FormatException)
            {
                return Error(BordereauErrorType.IncorrectFormat);
            }

            return null;
        }

        #region Typed Value Conversions

        internal string AsString()
        {
            return this.Value;
        }

        internal OptionSetValue AsOptionSet()
        {
            if (!this.HasValue)
                return null;

            // find matching option by value of attribute
            var matchingOption = this.TemplateColumn.OptionSets
                .FirstOrDefault(opt => opt.GetAttributeValue<string>("new_optionsetcode").ToLower() == this.Value.ToLower() ||
                                       opt.GetAttributeValue<string>("new_templatelabel").ToLower() == this.Value.ToLower());

            if (matchingOption == null)
                throw new FormatException("Option not found for value '{0}'".FormatWith(this.Value));

            // option set integer value is stored as a string
            var optionSetValue = matchingOption.GetAttributeValue<int>("new_optionsetvalue");
            return new OptionSetValue(optionSetValue);
        }

        internal DateTime? AsDateTime()
        {
            if (!this.HasValue)
                return null;

            DateTime date;
            if (!DateTime.TryParse(this.Value, ParsingCulture, DateTimeStyles.AssumeLocal, out date))
                throw new FormatException("Cannot convert value '{0}' to date. ".FormatWith(this.Value));

            return date;
        }

        internal decimal? AsDecimal()
        {
            if (!this.HasValue)
                return null;

            decimal decimalValue;

            if (!decimal.TryParse(this.Value, out decimalValue))
                throw new FormatException("Cannot convert value '{0}' to decimal number. ".FormatWith(this.Value));

            return decimalValue;
        }

        internal int? AsInteger()
        {
            if (!this.HasValue)
                return null;

            int intValue;

            if (!int.TryParse(this.Value, out intValue))
                throw new FormatException("Cannot convert value '{0}' to whole number. ".FormatWith(this.Value));

            return intValue;
        }

        internal Entity AsEntity(string retrieveByField = null)
        {
            if (!this.HasValue)
                return null;

            if (this.TemplateColumn.LookupMapping == LookupMapping.ByName)
            {
                Entity target;

                if (string.IsNullOrEmpty(retrieveByField))
                    target = this.TemplateColumn.RetrieveLookupByName(this.Value);
                else
                    target = this.TemplateColumn.RetrieveLookupByAttribute(retrieveByField, this.Value);

                return target;
            }
            else if (this.TemplateColumn.LookupMapping == LookupMapping.ByOptionSet)
            {
                var optSetVal = AsOptionSet();
                // convention: lookup field must be the same as entity name
                return this.TemplateColumn.RetrieveLookupByOptionSet(this.TemplateColumn.LookupTargetEntityName, optSetVal.Value);
            }

            return null;
        }

        internal bool? AsBool()
        {
            if (!this.HasValue)
                return null;
            
            if (this.TemplateColumn.OptionSets.Any())
            {
                // mapped by option set items
                var optSet = AsOptionSet();
                return optSet.Value != 0;
            }
            else
            {
                // naive mapping
                var valueUpper = this.Value.ToUpper();

                if (valueUpper == "YES" || valueUpper == "Y")
                    return true;
                else if (valueUpper == "NO" || valueUpper == "N")
                    return false;
                else
                    throw new FormatException("Cannot map value '{0}' to Boolean".FormatWith(this.Value));
            }
        }

        #endregion

        #region overloaded typecast operators
        // I am still not sure if this is a good idea, but it saves checks like:
        // emailAttr != null ? emailAttr.Value : null;

        public static explicit operator string(MappedAttribute a)
        {
            if (a == null)
                return null;

            return a.AsString();
        }

        public static explicit operator int?(MappedAttribute a)
        {
            if (a == null)
                return null;

            return a.AsInteger();
        }

        #endregion

        private BordereauError Error(BordereauErrorType type)
        {
            return new BordereauError(this.TemplateColumn, type, this.Value);
        }

        public override string ToString()
        {
            return "{0}.{1}{4} = '{2}'[{3}]"
                .FormatWith(TemplateColumn.EntityName, TemplateColumn.AttributeName, Value, Format, TemplateColumn.Mandatory ? "*" : "");
        }
    }
}
