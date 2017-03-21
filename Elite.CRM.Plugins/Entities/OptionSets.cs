using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Elite.CRM.Plugins.Entities
{
    // file for global option sets which do not belong to specific entities
    enum AddressOrigin
    {
        Manual = 100000000,
        Bordereaux = 100000001,
        PostalCodeSoftware = 100000002,
    }

    enum LobLevel
    {
        LobGroup = 100000000,
        Lob = 100000001,
        ProductLine = 100000002,
        Scheme = 100000003,
    }

    enum CustomEntityStatus
    {
        Active = 0,
        Inactive = 1,
    }

    #region Receipt

    enum ReceiptPaidBy
    {
        Broker = 100000000,
        PolicyHolder = 100000001
    }

    enum ReceiptPaymentStatus
    {
        Unpaid = 100000000,
        Paid = 100000001,
        PartPaid = 100000002,
        Overpaid = 100000003,
        Failed = 100000004,
        Cancelled = 100000005
    }

    enum ReceiptPaymentChannel
    {
        Direct = 100000000,
        Bordereau = 100000001,
        Internet = 100000002,
    }

    enum ReceiptPaymentMethod
    {
        BalanceTransfer = 100000000,
        BacsTransfer = 100000001,
        TelephoneTransfer = 100000002,
        Cheque = 100000003,
        CreditCard = 100000004,
        PaymentToBroker = 100000005,
    }

    #endregion
}
