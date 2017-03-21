using Elite.CRM.Plugins.Entities;
using Elite.CRM.Plugins.ErrorHandling;
using Microsoft.Xrm.Sdk;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Elite.CRM.Plugins.BordereauImport;

namespace Elite.CRM.Plugins.BordereauImport
{
    static class BordereauExtensions
    {
        #region Filtering IEnumerable of MappedAttribute/BordereauTemplateColumn

        /// <summary>
        /// Filters collection of Bordereau Template Columns based on logical name of entity.
        /// </summary>
        /// <param name="columns">Collection of BordereauTemplateColumn objects to filter.</param>
        /// <param name="entityLogicalName">Logical name of an entity.</param>
        /// <returns>Filtered collection of BordereauTemplateColumn objects which </returns>
        public static IEnumerable<BordereauTemplateColumn> ForEntity(this IEnumerable<BordereauTemplateColumn> columns, string entityLogicalName)
        {
            ThrowIf.Argument.IsNull(columns, "columns");
            ThrowIf.Argument.IsNull(entityLogicalName, "entityLogicalName");

            return columns.Where(c => c.EntityName == entityLogicalName);
        }

        /// <summary>
        /// Filters collection of mapped attributes based on logical name of entity.
        /// </summary>
        /// <param name="attrs">Collections of MappedAttribute objects to filter.</param>
        /// <param name="entityLogicalName">Logical name of an entity.</param>
        /// <returns>All attributes with entity logical name equal to supplied value.</returns>
        public static IEnumerable<MappedAttribute> ForEntity(this IEnumerable<MappedAttribute> attrs, string entityLogicalName)
        {
            ThrowIf.Argument.IsNull(attrs, "attrs");
            //ThrowIf.Argument.IsNullOrEmpty(entityLogicalName, "entityLogicalName");

            return attrs.Where(c => c.EntityName == entityLogicalName);
        }

        /// <summary>
        /// Filters collection of mapped attributes based on logical name of entity.
        /// </summary>
        /// <param name="attrs">Collections of MappedAttribute objects to filter.</param>
        /// <param name="entityLogicalNames">Array of logical names of an entities, which will be included in result.</param>
        /// <returns>All attributes with entity logical name included in supplied array.</returns>
        public static IEnumerable<MappedAttribute> ForEntities(this IEnumerable<MappedAttribute> attrs, params string[] entityLogicalNames)
        {
            ThrowIf.Argument.IsNull(attrs, "attrs");

            return attrs.Where(c => entityLogicalNames.Contains(c.EntityName));
        }

        /// <summary>
        /// Filters collection of mapped attributes based on logical name of attribute. 
        /// </summary>
        /// <param name="attrs">Collections of MappedAttribute objects to filter.</param>
        /// <param name="attributeName">Logical name of an attribute.</param>
        /// <returns>All attributes with attribute logical name equals supplied value.</returns>
        public static IEnumerable<MappedAttribute> ForAttribute(this IEnumerable<MappedAttribute> attrs, string attributeName)
        {
            ThrowIf.Argument.IsNull(attrs, "attrs");
            ThrowIf.Argument.IsNullOrEmpty(attributeName, "attributeName");

            return attrs.Where(c => c.AttributeName == attributeName);
        }

        /// <summary>
        /// Excludes all MappedAttributes with AttributeName matching with specified names to exclude.
        /// </summary>
        /// <param name="attrs">Collections of MappedAttribute objects to filter.</param>
        /// <param name="toExclude">Names of fields to exclude from MappedAttribute collection.</param>
        /// <returns></returns>
        public static IEnumerable<MappedAttribute> ExcludeAttributes(this IEnumerable<MappedAttribute> attrs, params string[] attributesToExclude)
        {
            ThrowIf.Argument.IsNull(attrs, "attrs");

            if (attributesToExclude == null || attributesToExclude.Length == 0)
                return attrs;

            return attrs.Where(a => !attributesToExclude.Contains(a.AttributeName));
        }

        /// <summary>
        /// Filters collection of mapped attributes based on Id of a Role type. 
        /// </summary>
        /// <param name="attrs">Collections of MappedAttribute objects to filter.</param>
        /// <param name="roleTypeId">Id of the Role type.</param>
        /// <returns>All attributes with Role type id equal to supplied value.</returns>
        public static IEnumerable<MappedAttribute> ForRoleType(this IEnumerable<MappedAttribute> attrs, Guid? roleTypeId)
        {
            ThrowIf.Argument.IsNull(attrs, "attrs");
            return attrs.Where(c =>
            {
                if (c.TemplateColumn.RoleTypeRef == null && roleTypeId == null)
                    return true;
                else if (c.TemplateColumn.RoleTypeRef != null && roleTypeId == c.TemplateColumn.RoleTypeRef.Id)
                    return true;
                return false;
            });
        }

        /// <summary>
        /// Filters collection of mapped attributes based on AddressOf field. 
        /// </summary>
        /// <remarks>
        /// AddressOf attribute denotes to which entity current address fields belong to (Account or Insured Risk). 
        /// </remarks>
        /// <param name="attrs">Collections of MappedAttribute objects to filter.</param>
        /// <param name="addressOf">Value of AddressOf field for which attributes will be filtered.</param>
        /// <returns>All attributes with AddressOf field equal to supplied value.</returns>
        public static IEnumerable<MappedAttribute> ForAddressOf(this IEnumerable<MappedAttribute> attrs, AddressOf addressOf)
        {
            ThrowIf.Argument.IsNull(attrs, "attrs");

            // addresses for role and policy holder is a default one - both null and RoleOrPolicyHolder are valid in this case
            if (addressOf == AddressOf.RoleOrPolicyHolder)
                return attrs.Where(a => a.TemplateColumn.AddressOf == null || a.TemplateColumn.AddressOf == AddressOf.RoleOrPolicyHolder);

            return attrs.Where(a => a.TemplateColumn.AddressOf == addressOf);
        }

        /// <summary>
        /// Filters collection of mapped attributes based on Cover id. 
        /// </summary>
        /// <param name="attrs">Collections of MappedAttribute objects to filter.</param>
        /// <param name="coverId">Id of cover.</param>
        /// <returns>All attributes with Cover id equal to supplied value.</returns>
        public static IEnumerable<MappedAttribute> ForCover(this IEnumerable<MappedAttribute> attrs, Guid? coverId)
        {
            ThrowIf.Argument.IsNull(attrs, "attrs");
            return attrs.Where(c =>
            {
                if (c.TemplateColumn.CoverRef == null && coverId == null)
                    return true;
                else if (c.TemplateColumn.CoverRef != null && coverId == c.TemplateColumn.CoverRef.Id)
                    return true;
                return false;
            });
        }

        /// <summary>
        /// Splits mapped attributes into groups by role number field. Then executes action for each group.
        /// </summary>
        /// <remarks>
        /// Make sure to supply only attributes for a single role type id. If collection contains attributes for multiple roles, 
        /// delegate will be executed for 'mixed' group (e.g. Sub-agent 2 + Solicitor 2), which might not be intended.
        /// </remarks>
        /// <param name="attrs">Collections of MappedAttribute objects to group by role number.</param>
        /// <param name="action">Delegate, which gets executed for each group of columns </param.
        public static void ForEachRoleNumber(this IEnumerable<MappedAttribute> attrs, Action<int?, IEnumerable<MappedAttribute>> action)
        {
            ThrowIf.Argument.IsNull(attrs, "attrs");

            if (action == null)
                return;

            var uniqueNumberGroups = attrs
                .GroupBy(a => a.TemplateColumn.RoleNumber);

            foreach (var group in uniqueNumberGroups)
            {
                if (!group.Any())
                    continue;

                action(group.Key, group);
            }
        }

        #endregion

        #region More specific Bordereau columns processing

        /// <summary>
        /// Validates each attribute in collection, returns collection of all errors.
        /// </summary>
        /// <param name="attrs">Collection of attributes to validate.</param>
        /// <returns>Collection of all validation errors. If all attributes are valid, returns empty collection.</returns>
        public static IEnumerable<BordereauError> Validate(this IEnumerable<MappedAttribute> attrs)
        {
            ThrowIf.Argument.IsNull(attrs, "attrs");

            // no attributes -> valid
            if (!attrs.Any())
                yield break;

            foreach (var attriute in attrs)
            {
                var error = attriute.ValidateValue();
                if (error != null)
                    yield return error;
            }
        }

        /// <summary>
        /// Processes role party, either account or contact, and their address. 
        /// Result: 
        ///   - create or update contact/account, based on which data is present in columns
        ///   - create new address record for contact/account, if address is present in columns
        ///   
        /// </summary>
        /// <param name="svc"></param>
        /// <param name="defaultCountry"></param>
        /// <returns>Entity reference of currently created/updated role party.</returns>
        public static EntityReference ProcessParty(this IOrganizationService svc, IEnumerable<MappedAttribute> attributes, EntityReference defaultCountry)
        {
            ThrowIf.Argument.IsNull(svc, "svc");

            // no attributes to process, returning
            if (!attributes.Any())
                return null;

            // if party entity is null, it means party fields are empty. Nothing will be done.
            var partyEntityName = attributes.ContactOrAccount();
            if (partyEntityName == null)
                return null;

            var addressAttrs = attributes
                .ForEntity("new_address")
                .ForAddressOf(AddressOf.RoleOrPolicyHolder);

            CompositeAddress compositeAddress = new CompositeAddress();

            if (addressAttrs.Any())
                compositeAddress = svc.ProcessAddress(addressAttrs, defaultCountry);

            // checks for existing account/contact
            Entity roleParty = null;
            if (partyEntityName == "account")
            {
                var accountAttrs = attributes
                    .ForEntity("account");

                var accountName = accountAttrs
                    .ForAttribute("name")
                    .FirstOrDefault()
                    .AsString();

                roleParty = svc.RetrieveAccountByName(accountName);

                if (roleParty == null)
                {
                    //context.Trace("Account not found, creating new account.");
                    roleParty = new Entity("account");
                }
            }
            else if (partyEntityName == "contact")
            {
                var contactAttrs = attributes.ForEntity("contact");

                var firstNameAttr = contactAttrs
                    .ForAttribute("firstname")
                    .FirstOrDefault();

                var lastNameAttr = contactAttrs
                    .ForAttribute("lastname")
                    .FirstOrDefault();

                var dateOfBirthAttr = contactAttrs
                    .ForAttribute("birthdate")
                    .FirstOrDefault();

                var emailAttr = contactAttrs
                    .ForAttribute("emailaddress1")
                    .FirstOrDefault();

                var mobilePhoneAttr = contactAttrs
                    .ForAttribute("mobilephone")
                    .FirstOrDefault();

                var niNumberAttr = contactAttrs
                    .ForAttribute("new_clientninumber")
                    .FirstOrDefault();

                // try to pick both: contact.new_postalcode and new_address.postalcode - there might be the case 
                var postalCodeAttr = contactAttrs.ForAttribute("new_postalcode").FirstOrDefault() ??
                    addressAttrs.ForAttribute("new_postalcode").FirstOrDefault();

                var firstName = firstNameAttr != null ? firstNameAttr.Value : null;
                var lastName = lastNameAttr != null ? lastNameAttr.Value : null;

                var dateOfBirth = dateOfBirthAttr != null ? dateOfBirthAttr.AsDateTime() : null;
                var email = (string)emailAttr;

                var mobile = (string)mobilePhoneAttr;
                var niNumber = (string)niNumberAttr;

                var postalCode = (string)postalCodeAttr;

                var existingContacts = svc.RetrieveContactByName(firstName, lastName, postalCode, dateOfBirth, email, mobile, niNumber);

                // exactly one match, we take an existing contact
                if (existingContacts.Count() == 1)
                {
                    roleParty = existingContacts.First();
                }

                if (roleParty == null)
                {
                    roleParty = new Entity("contact");
                }
            }

            // We have to create address if:
            // - role party is not created yet, obviously their address cannot be in the system
            // - role party exists, but has no existing address  
            if (roleParty.Id == Guid.Empty || roleParty.GetAttributeValue<EntityReference>("new_address") == null)
            {
                if (compositeAddress == null)
                    throw new BordereauImport.BordereauException(BordereauExceptionType.DataError, "Postcode cannot be null");

                if (compositeAddress.Address != null)
                {
                    if (compositeAddress.Address.Id == Guid.Empty)
                        compositeAddress.Address.Id = svc.Create(compositeAddress.Address);

                    roleParty["new_address"] = compositeAddress.Address.ToEntityReference();
                }
            }
            else
            {
                // otherwise, we just update existing address record associated with role party
                // with data from bordereau
                if (compositeAddress == null)
                    return null;
                if (compositeAddress.Address != null)
                {
                    if (compositeAddress.Address.Id == Guid.Empty)
                        compositeAddress.Address.Id = roleParty.GetAttributeValue<EntityReference>("new_address").Id;

                    svc.Update(compositeAddress.Address);
                }
            }

            roleParty.UpdateWithAttributes(attributes.ForEntity(partyEntityName));

            if (compositeAddress.PostalCode != null)
                roleParty["new_postalcode"] = compositeAddress.PostalCode;

            if (compositeAddress.Country != null)
                roleParty["new_countryid"] = compositeAddress.Country;

            svc.CreateOrUpdateEntity(ref roleParty);

            return roleParty.ToEntityReference();
        }

        public static CompositeAddress ProcessAddress(this IOrganizationService svc, IEnumerable<MappedAttribute> addressAttrs, EntityReference defaultCountry = null)
        {
            var postalCodeAttr = addressAttrs
                .ForAttribute("new_postalcode")
                .FirstOrDefault();

            // no postal code means no address information
            if (postalCodeAttr == null || !postalCodeAttr.HasValue)
                return null;

            #region resolve country

            EntityReference countryRef = null;

            var countryAttr = addressAttrs
                .ForAttribute("new_country")
                .FirstOrDefault();

            if (defaultCountry != null)
            {
                countryRef = defaultCountry;
            }
            else if (countryAttr != null)
            {
                // TODO search by country code?
                var countryEntity = countryAttr.AsEntity();
                if (countryEntity != null)
                    countryRef = countryEntity.ToEntityReference();
            }
            else
            {
                // if selecting country fails, it's not possible to create a valid address
                throw BordereauException.TemplateError("Country information is missing or in incorrect format.");
            }

            #endregion

            #region resolve postal code

            Entity postalCodeEntity = svc.SearchPostalCode(postalCodeAttr.AsString(), countryRef.Id);

            if (postalCodeEntity == null)
            {
                postalCodeEntity = new Entity("new_postalcode");
                postalCodeEntity["new_name"] = postalCodeAttr.AsString();
                postalCodeEntity["new_country"] = countryRef;
                postalCodeEntity.Id = svc.Create(postalCodeEntity);
            }

            var postalCodeRef = postalCodeEntity.ToEntityReference();

            #endregion

            Entity address = null;

            // check if there are any more attributes than just postal code and country
            if (addressAttrs.ExcludeAttributes("new_country", "new_postalcode").Any())
            {
                var street1Attr = addressAttrs
                    .ForAttribute("new_street1")
                    .FirstOrDefault();

                var numberAttr = addressAttrs
                    .ForAttribute("new_addressnumbertext")
                    .FirstOrDefault();

                var buildingAttr = addressAttrs
                   .ForAttribute("new_addressname")
                   .FirstOrDefault();

                // try to find an existing address based on street, house no. and building information
                address = svc.MatchAddress(postalCodeRef.Id, (string)street1Attr, (string)numberAttr, (string)buildingAttr);

                // if address is not found, create a new record with data from Bx row
                if (address == null)
                {
                    address = new Entity("new_address");
                    address.UpdateWithAttributes(addressAttrs.ExcludeAttributes("new_country", "new_postalcode"));
                    address["new_addressorigin"] = AddressOrigin.Bordereaux.ToOptionSet();
                    address["new_country"] = countryRef;
                    address["new_postalcode"] = postalCodeRef;
                }
            }   

            return new CompositeAddress()
            {
                Country = countryRef,
                PostalCode = postalCodeRef,
                Address = address
            };
        }

        #endregion

        internal class CompositeAddress
        {
            public EntityReference Country { get; set; }
            public EntityReference PostalCode { get; set; }
            public Entity Address { get; set; }
        }

    }
}
