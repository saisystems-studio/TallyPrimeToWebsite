using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using TallyWebAPI.Models;

namespace TallyWebAPI.Services
{
    public class OutstandingService
    {
        private readonly HttpClient _httpClient;

        public OutstandingService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        // =========================================================
        // 1. LEDGER OUTSTANDING RAW
        //    Opening + Closing + Opening Bill Allocations
        // =========================================================
        public async Task<string> GetOutstandingRawAsync(
            string companyName)
        {
            var safeCompanyName =
                System.Security.SecurityElement.Escape(companyName) ?? "";

            var xmlRequest = $"""
            <ENVELOPE>
                <HEADER>
                    <VERSION>1</VERSION>
                    <TALLYREQUEST>Export</TALLYREQUEST>
                    <TYPE>Collection</TYPE>
                    <ID>OutstandingLedgerCollection</ID>
                </HEADER>

                <BODY>
                    <DESC>
                        <STATICVARIABLES>
                            <SVEXPORTFORMAT>$$SysName:XML</SVEXPORTFORMAT>
                            <SVCURRENTCOMPANY>{safeCompanyName}</SVCURRENTCOMPANY>
                        </STATICVARIABLES>

                        <TDL>
                            <TDLMESSAGE>

                                <COLLECTION NAME="OutstandingLedgerCollection">
                                    <TYPE>Ledger</TYPE>

                                    <FETCH>Name</FETCH>
                                    <FETCH>Parent</FETCH>
                                    <FETCH>OpeningBalance</FETCH>
                                    <FETCH>ClosingBalance</FETCH>
                                    <FETCH>IsBillWiseOn</FETCH>

                                    <FETCH>BillAllocations.*</FETCH>
                                </COLLECTION>

                            </TDLMESSAGE>
                        </TDL>
                    </DESC>
                </BODY>
            </ENVELOPE>
            """;

            return await TallyXmlTransport.PostAsync(
                _httpClient,
                xmlRequest
            );
        }


        // =========================================================
        // 2. SINGLE LEDGER OPENING BILL RAW TEST
        // =========================================================
        public async Task<string> GetOpeningBillsRawAsync(
            string companyName,
            string ledgerName)
        {
            var safeCompanyName =
                System.Security.SecurityElement.Escape(companyName) ?? "";

            var safeLedgerName =
                System.Security.SecurityElement.Escape(ledgerName) ?? "";

            var xmlRequest = $"""
            <ENVELOPE>
                <HEADER>
                    <VERSION>1</VERSION>
                    <TALLYREQUEST>Export</TALLYREQUEST>
                    <TYPE>Collection</TYPE>
                    <ID>OpeningBillLedgerCollection</ID>
                </HEADER>

                <BODY>
                    <DESC>
                        <STATICVARIABLES>
                            <SVEXPORTFORMAT>$$SysName:XML</SVEXPORTFORMAT>
                            <SVCURRENTCOMPANY>{safeCompanyName}</SVCURRENTCOMPANY>
                        </STATICVARIABLES>

                        <TDL>
                            <TDLMESSAGE>

                                <COLLECTION NAME="OpeningBillLedgerCollection">
                                    <TYPE>Ledger</TYPE>

                                    <FILTER>OpeningBillLedgerFilter</FILTER>

                                    <FETCH>Name</FETCH>
                                    <FETCH>Parent</FETCH>
                                    <FETCH>OpeningBalance</FETCH>
                                    <FETCH>ClosingBalance</FETCH>
                                    <FETCH>IsBillWiseOn</FETCH>
                                    <FETCH>BillAllocations.*</FETCH>
                                </COLLECTION>

                                <SYSTEM TYPE="Formulae"
                                        NAME="OpeningBillLedgerFilter">
                                    $Name = "{safeLedgerName}"
                                </SYSTEM>

                            </TDLMESSAGE>
                        </TDL>
                    </DESC>
                </BODY>
            </ENVELOPE>
            """;

            return await TallyXmlTransport.PostAsync(
                _httpClient,
                xmlRequest
            );
        }


        // =========================================================
        // 3. VOUCHER + BILL ALLOCATION RAW
        // =========================================================
        public async Task<string> GetVoucherBillsRawAsync(
            string companyName,
            string fromDate,
            string toDate)
        {
            var safeCompanyName =
                System.Security.SecurityElement.Escape(companyName) ?? "";

            var xmlRequest = $"""
            <ENVELOPE>
                <HEADER>
                    <VERSION>1</VERSION>
                    <TALLYREQUEST>Export</TALLYREQUEST>
                    <TYPE>Collection</TYPE>
                    <ID>OutstandingVoucherCollection</ID>
                </HEADER>

                <BODY>
                    <DESC>
                        <STATICVARIABLES>
                            <SVEXPORTFORMAT>$$SysName:XML</SVEXPORTFORMAT>
                            <SVCURRENTCOMPANY>{safeCompanyName}</SVCURRENTCOMPANY>
                            <SVFROMDATE TYPE="Date">{fromDate}</SVFROMDATE>
                            <SVTODATE TYPE="Date">{toDate}</SVTODATE>
                        </STATICVARIABLES>

                        <TDL>
                            <TDLMESSAGE>

                                <COLLECTION NAME="OutstandingVoucherCollection">
                                    <TYPE>Voucher</TYPE>

                                    <FETCH>Date</FETCH>
                                    <FETCH>VoucherNumber</FETCH>
                                    <FETCH>VoucherTypeName</FETCH>
                                    <FETCH>PartyLedgerName</FETCH>
                                    <FETCH>GUID</FETCH>

                                    <FETCH>AllLedgerEntries.*</FETCH>
                                    <FETCH>
                                        AllLedgerEntries.BillAllocations.*
                                    </FETCH>
                                </COLLECTION>

                            </TDLMESSAGE>
                        </TDL>
                    </DESC>
                </BODY>
            </ENVELOPE>
            """;

            return await TallyXmlTransport.PostAsync(
                _httpClient,
                xmlRequest
            );
        }


        // =========================================================
        // 4. FINAL OUTSTANDING
        //    Opening Bills + Voucher Bill Allocations + On Account
        // =========================================================
        public async Task<List<OutstandingEntity>>
            GetOutstandingAsync(
                string companyName,
                string fromDate,
                string toDate)
        {
            var result = new List<OutstandingEntity>();


            // =====================================================
            // A. OPENING BILL ALLOCATIONS
            // =====================================================
            var ledgerXml =
                await GetOutstandingRawAsync(companyName);

            if (!string.IsNullOrWhiteSpace(ledgerXml))
            {
                ledgerXml = CleanTallyXml(ledgerXml);

                var ledgerDocument =
                    XDocument.Parse(ledgerXml);

                var ledgers = ledgerDocument
                    .Descendants()
                    .Where(x =>
                        x.Name.LocalName.Equals(
                            "LEDGER",
                            StringComparison.OrdinalIgnoreCase
                        )
                    );

                foreach (var ledger in ledgers)
                {
                    var ledgerName =
                        GetLedgerName(ledger);

                    if (string.IsNullOrWhiteSpace(ledgerName))
                        continue;

                    var isBillWise =
                        GetDirectValue(
                            ledger,
                            "ISBILLWISEON"
                        );

                    if (!isBillWise.Equals(
                            "Yes",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var openingBills = ledger
                        .Elements()
                        .Where(x =>
                            x.Name.LocalName.Equals(
                                "BILLALLOCATIONS.LIST",
                                StringComparison.OrdinalIgnoreCase
                            )
                        );

                    foreach (var bill in openingBills)
                    {
                        var billReference =
                            GetDirectValue(
                                bill,
                                "NAME"
                            );

                        if (string.IsNullOrWhiteSpace(billReference))
                            continue;

                        var openingAmountText =
                            GetDirectValue(
                                bill,
                                "OPENINGBALANCE"
                            )
                            .Replace(",", "")
                            .Trim();

                        if (!decimal.TryParse(
                            openingAmountText,
                            NumberStyles.Any,
                            CultureInfo.InvariantCulture,
                            out var openingAmount))
                        {
                            continue;
                        }

                        if (openingAmount == 0)
                            continue;

                        var billDate =
                            GetDirectValue(
                                bill,
                                "BILLDATE"
                            );

                        var creditPeriod =
                            GetDirectValue(
                                bill,
                                "BILLCREDITPERIOD"
                            );

                        // Opening balance has no Tally voucher GUID.
                        // Stable synthetic key prevents duplicates.
                        var openingKey =
                            $"OPENING|{ledgerName}|{billReference}";

                        result.Add(
                            new OutstandingEntity
                            {
                                LedgerName = ledgerName,

                                TallyGuid = openingKey,

                                VoucherNumber = null,
                                VoucherType = "Opening Balance",
                                VoucherDate = billDate,

                                BillReference = billReference,
                                BillType = "Opening Balance",
                                BillDate = billDate,
                                CreditPeriod = creditPeriod,

                                Amount = openingAmount,

                                LastSyncedAt = DateTime.UtcNow
                            }
                        );
                    }
                }
            }


            // =====================================================
            // B. VOUCHER BILL ALLOCATIONS
            //    Includes:
            //    New Ref
            //    Agst Ref
            //    On Account
            // =====================================================
            var voucherXml =
                await GetVoucherBillsRawAsync(
                    companyName,
                    fromDate,
                    toDate
                );

            if (string.IsNullOrWhiteSpace(voucherXml))
                return result;

            voucherXml = CleanTallyXml(voucherXml);

            var voucherDocument =
                XDocument.Parse(voucherXml);

            var vouchers = voucherDocument
                .Descendants()
                .Where(x =>
                    x.Name.LocalName.Equals(
                        "VOUCHER",
                        StringComparison.OrdinalIgnoreCase
                    )
                );

            foreach (var voucher in vouchers)
            {
                var tallyGuid =
                    GetValue(
                        voucher,
                        "GUID"
                    );

                if (string.IsNullOrWhiteSpace(tallyGuid))
                    continue;


                var voucherNumber =
                    GetValue(
                        voucher,
                        "VOUCHERNUMBER"
                    );


                var voucherType =
                    GetValue(
                        voucher,
                        "VOUCHERTYPENAME"
                    );


                var voucherDate =
                    GetValue(
                        voucher,
                        "DATE"
                    );


                var partyName =
                    GetValue(
                        voucher,
                        "PARTYLEDGERNAME"
                    );


                if (string.IsNullOrWhiteSpace(partyName))
                    continue;


                // Parse only direct ALLLEDGERENTRIES
                // matching the voucher party.
                var partyEntries = voucher
                    .Elements()
                    .Where(x =>
                        x.Name.LocalName.Equals(
                            "ALLLEDGERENTRIES.LIST",
                            StringComparison.OrdinalIgnoreCase
                        )
                    )
                    .Where(x =>
                        GetDirectValue(
                            x,
                            "LEDGERNAME"
                        )
                        .Equals(
                            partyName,
                            StringComparison.OrdinalIgnoreCase
                        )
                    );


                foreach (var partyEntry in partyEntries)
                {
                    var billAllocations = partyEntry
                        .Elements()
                        .Where(x =>
                            x.Name.LocalName.Equals(
                                "BILLALLOCATIONS.LIST",
                                StringComparison.OrdinalIgnoreCase
                            )
                        );


                    foreach (var bill in billAllocations)
                    {
                        // ---------------------------------------------
                        // BILL TYPE FIRST
                        // ---------------------------------------------
                        var billType =
                            GetDirectValue(
                                bill,
                                "BILLTYPE"
                            );


                        // ---------------------------------------------
                        // BILL REFERENCE
                        // ---------------------------------------------
                        var billReference =
                            GetDirectValue(
                                bill,
                                "NAME"
                            );


                        // Tally On Account allocations can have
                        // no bill reference.
                        //
                        // Do NOT attach them to an existing bill.
                        // Store as a separate ledger-level adjustment.
                        //
                        // Tally GUID makes this reference stable
                        // and unique for sync.
                        if (string.IsNullOrWhiteSpace(billReference))
                        {
                            if (billType.Equals(
                                "On Account",
                                StringComparison.OrdinalIgnoreCase))
                            {
                                billReference =
                                    $"ONACCOUNT|{tallyGuid}";
                            }
                            else
                            {
                                continue;
                            }
                        }


                        // ---------------------------------------------
                        // BILL DATE
                        // ---------------------------------------------
                        var billDate =
                            GetDirectValue(
                                bill,
                                "BILLDATE"
                            );

                        if (string.IsNullOrWhiteSpace(billDate))
                        {
                            billDate = voucherDate;
                        }


                        // ---------------------------------------------
                        // CREDIT PERIOD
                        // ---------------------------------------------
                        var creditPeriod =
                            GetDirectValue(
                                bill,
                                "BILLCREDITPERIOD"
                            );


                        // ---------------------------------------------
                        // AMOUNT
                        // ---------------------------------------------
                        var amountText =
                            GetDirectValue(
                                bill,
                                "AMOUNT"
                            )
                            .Replace(",", "")
                            .Trim();


                        if (!decimal.TryParse(
                            amountText,
                            NumberStyles.Any,
                            CultureInfo.InvariantCulture,
                            out var amount))
                        {
                            continue;
                        }


                        if (amount == 0)
                            continue;


                        // ---------------------------------------------
                        // ADD RESULT
                        // ---------------------------------------------
                        result.Add(
                            new OutstandingEntity
                            {
                                LedgerName = partyName,

                                TallyGuid = tallyGuid,

                                VoucherNumber = voucherNumber,
                                VoucherType = voucherType,
                                VoucherDate = voucherDate,

                                BillReference = billReference,
                                BillType = billType,

                                BillDate = billDate,
                                CreditPeriod = creditPeriod,

                                Amount = amount,

                                LastSyncedAt = DateTime.UtcNow
                            }
                        );
                    }
                }
            }


            return result;
        }


        // =========================================================
        // CLEAN TALLY XML
        // =========================================================
        private static string CleanTallyXml(
            string xml)
        {
            // Remove XML-invalid Tally control character references.
            xml = Regex.Replace(
                xml,
                @"&#(?:0?[0-8]|0?1[0-9]|0?2[0-9]|3[01]);",
                ""
            );

            // Remove UDF namespace prefix where no namespace
            // declaration is available in returned XML.
            xml = Regex.Replace(
                xml,
                @"(<\/?)UDF:",
                "$1",
                RegexOptions.IgnoreCase
            );

            return xml;
        }


        // =========================================================
        // GET LEDGER NAME
        // =========================================================
        private static string GetLedgerName(
            XElement ledger)
        {
            var nameAttribute =
                ledger.Attribute("NAME")
                    ?.Value
                    ?.Trim();

            if (!string.IsNullOrWhiteSpace(nameAttribute))
                return nameAttribute;

            return GetDirectValue(
                ledger,
                "NAME"
            );
        }


        // =========================================================
        // HELPERS
        // =========================================================
        private static string GetValue(
            XElement parent,
            string name)
        {
            return parent
                .Descendants()
                .FirstOrDefault(x =>
                    x.Name.LocalName.Equals(
                        name,
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                ?.Value
                ?.Trim() ?? "";
        }


        private static string GetDirectValue(
            XElement parent,
            string name)
        {
            return parent
                .Elements()
                .FirstOrDefault(x =>
                    x.Name.LocalName.Equals(
                        name,
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                ?.Value
                ?.Trim() ?? "";
        }
    }
}