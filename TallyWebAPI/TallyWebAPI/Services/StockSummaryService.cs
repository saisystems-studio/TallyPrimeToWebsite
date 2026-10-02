using System.Text;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using TallyWebAPI.DTOs;

namespace TallyWebAPI.Services
{
    public class StockSummaryService
    {
        private readonly HttpClient _httpClient;

        public StockSummaryService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<string> GetRawStockSummaryAsync(string fromDate, string toDate, string companyName)
        {
            var safeCompanyName = System.Security.SecurityElement.Escape(companyName) ?? "";

            var xmlRequest = $"""
    <ENVELOPE>
        <HEADER>
            <VERSION>1</VERSION>
            <TALLYREQUEST>Export</TALLYREQUEST>
            <TYPE>Collection</TYPE>
            <ID>StockSummaryCollection</ID>
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
                        <COLLECTION NAME="StockSummaryCollection">
                            <TYPE>StockItem</TYPE>

                            <FETCH>Name</FETCH>
                            <FETCH>Parent</FETCH>
                            <FETCH>BaseUnits</FETCH>

                            <FETCH>OpeningBalance</FETCH>
                            <FETCH>ClosingBalance</FETCH>

                            <FETCH>GUID</FETCH>
                            <FETCH>MasterID</FETCH>
                            <FETCH>AlterID</FETCH>

                            <FETCH>OpeningValue</FETCH>
                            <FETCH>ClosingValue</FETCH>
                        </COLLECTION>
                    </TDLMESSAGE>
                </TDL>
            </DESC>
        </BODY>
    </ENVELOPE>
    """;

            return await TallyXmlTransport.PostAsync(
                _httpClient,
                xmlRequest);
        }

        public async Task<string> GetRawStockMovementsAsync(
    string fromDate,
    string toDate)
        {
            var xmlRequest = $"""
    <ENVELOPE>
        <HEADER>
            <VERSION>1</VERSION>
            <TALLYREQUEST>Export</TALLYREQUEST>
            <TYPE>Collection</TYPE>
            <ID>StockMovementCollection</ID>
        </HEADER>

        <BODY>
            <DESC>
                <STATICVARIABLES>
                    <SVEXPORTFORMAT>$$SysName:XML</SVEXPORTFORMAT>
                    <SVFROMDATE TYPE="Date">{fromDate}</SVFROMDATE>
                    <SVTODATE TYPE="Date">{toDate}</SVTODATE>
                </STATICVARIABLES>

                <TDL>
                    <TDLMESSAGE>

                        <COLLECTION NAME="StockMovementCollection">
                            <TYPE>Voucher</TYPE>

                            <FETCH>Date</FETCH>
                            <FETCH>VoucherTypeName</FETCH>
                            <FETCH>VoucherNumber</FETCH>

                            <FETCH>AllInventoryEntries.StockItemName</FETCH>
                            <FETCH>AllInventoryEntries.ActualQty</FETCH>
                            <FETCH>AllInventoryEntries.BilledQty</FETCH>
                            <FETCH>AllInventoryEntries.IsDeemedPositive</FETCH>
                        </COLLECTION>

                    </TDLMESSAGE>
                </TDL>
            </DESC>
        </BODY>
    </ENVELOPE>
    """;

            return await TallyXmlTransport.PostAsync(_httpClient, xmlRequest);
        }


        public async Task<StockSummaryDto> GetStockSummaryAsync(string fromDate, string toDate, string companyName)
        {
            var stockXml = await GetRawStockSummaryAsync(fromDate, toDate, companyName);

            var movementXml = await GetRawStockMovementsAsync(
                fromDate,
                toDate
            );

            stockXml = CleanXml(stockXml);
            movementXml = CleanXml(movementXml);

            var stockDocument = XDocument.Parse(stockXml);
            var movementDocument = XDocument.Parse(movementXml);

            var report = new StockSummaryDto
            {
                FromDate = fromDate,
                ToDate = toDate
            };

            var items = new Dictionary<
                string,
                StockSummaryItemDto
            >(StringComparer.OrdinalIgnoreCase);

            // -----------------------------------------
            // 1. STOCK ITEM MASTER / BALANCE DATA
            // -----------------------------------------

            var stockItems = stockDocument
                .Descendants()
                .Where(x =>
                    x.Name.LocalName.Equals(
                        "STOCKITEM",
                        StringComparison.OrdinalIgnoreCase
                    )
                );

            foreach (var stockItem in stockItems)
            {
                var name =
                    stockItem.Attribute("NAME")?.Value?.Trim();

                if (string.IsNullOrWhiteSpace(name))
                {
                    name = GetValue(stockItem, "NAME");
                }

                if (string.IsNullOrWhiteSpace(name))
                    continue;

                var group = GetValue(
                    stockItem,
                    "PARENT"
                );

                var unit = GetValue(
                    stockItem,
                    "BASEUNITS"
                );

                var opening = ParseQuantity(
                    GetValue(
                        stockItem,
                        "OPENINGBALANCE"
                    )
                );

                var closing = ParseQuantity(
                    GetValue(
                        stockItem,
                        "CLOSINGBALANCE"
                    )
                );

                items[name] = new StockSummaryItemDto
                {
                    StockItemName = name,
                    StockGroup = group,
                    Unit = unit,
                    OpeningQuantity = opening,
                    ClosingQuantity = closing
                };
            }

            // -----------------------------------------
            // 2. VOUCHER INVENTORY MOVEMENTS
            // -----------------------------------------

            var inventoryEntries = movementDocument
                .Descendants()
                .Where(x =>
                    x.Name.LocalName.Equals(
                        "ALLINVENTORYENTRIES.LIST",
                        StringComparison.OrdinalIgnoreCase
                    )
                );

            foreach (var entry in inventoryEntries)
            {
                var itemName = GetValue(
                    entry,
                    "STOCKITEMNAME"
                );

                if (string.IsNullOrWhiteSpace(itemName))
                    continue;

                if (!items.TryGetValue(
                        itemName,
                        out var item))
                {
                    continue;
                }

                var quantityText = GetValue(
                    entry,
                    "ACTUALQTY"
                );

                var quantity = Math.Abs(
                    ParseQuantity(quantityText)
                );

                if (quantity == 0)
                    continue;

                var isDeemedPositive = GetValue(
                    entry,
                    "ISDEEMEDPOSITIVE"
                );

                if (isDeemedPositive.Equals(
                        "Yes",
                        StringComparison.OrdinalIgnoreCase))
                {
                    item.InwardQuantity += quantity;
                }
                else if (isDeemedPositive.Equals(
                             "No",
                             StringComparison.OrdinalIgnoreCase))
                {
                    item.OutwardQuantity += quantity;
                }
            }

            // -----------------------------------------
            // 3. GROUP ITEMS
            // -----------------------------------------

            var groupedItems = items.Values
                .GroupBy(x =>
                    string.IsNullOrWhiteSpace(x.StockGroup)
                        ? "Primary"
                        : x.StockGroup
                )
                .OrderBy(x => x.Key);

            foreach (var group in groupedItems)
            {
                var groupDto = new StockSummaryGroupDto
                {
                    GroupName = group.Key,
                    Items = group
                        .OrderBy(x => x.StockItemName)
                        .ToList()
                };

                /*
                 * Only total quantities when the group
                 * uses the same unit.
                 *
                 * We must not add kg + PCS + pkt together.
                 */
                var units = groupDto.Items
                    .Select(x => x.Unit)
                    .Where(x =>
                        !string.IsNullOrWhiteSpace(x))
                    .Distinct(
                        StringComparer.OrdinalIgnoreCase
                    )
                    .ToList();

                if (units.Count == 1)
                {
                    groupDto.Unit = units[0];

                    groupDto.OpeningQuantity =
                        groupDto.Items.Sum(
                            x => x.OpeningQuantity
                        );

                    groupDto.InwardQuantity =
                        groupDto.Items.Sum(
                            x => x.InwardQuantity
                        );

                    groupDto.OutwardQuantity =
                        groupDto.Items.Sum(
                            x => x.OutwardQuantity
                        );

                    groupDto.ClosingQuantity =
                        groupDto.Items.Sum(
                            x => x.ClosingQuantity
                        );
                }

                report.Groups.Add(groupDto);
            }

            return report;
        }

        private static decimal ParseQuantity(
            string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return 0;

            /*
             * Examples:
             *
             * 1,292.000 kg
             * (-)5,986.000 kg
             * 891 PCS
             * (-)3 pkt
             */

            var text = value
                .Trim()
                .Replace(",", "");

            var negative =
                text.Contains("(-)") ||
                text.StartsWith("-");

            text = text.Replace("(-)", "");

            var match = Regex.Match(
                text,
                @"[-+]?\d+(?:\.\d+)?"
            );

            if (!match.Success)
                return 0;

            if (!decimal.TryParse(
                    match.Value,
                    NumberStyles.Any,
                    CultureInfo.InvariantCulture,
                    out var quantity))
            {
                return 0;
            }

            quantity = Math.Abs(quantity);

            return negative
                ? -quantity
                : quantity;
        }

        private static string CleanXml(
            string xml)
        {
            xml = Regex.Replace(
                xml,
                @"&#(?:0?[0-8]|0?1[0-9]|0?2[0-9]|3[01]);",
                ""
            );

            xml = Regex.Replace(
                xml,
                @"(<\/?)UDF:",
                "$1"
            );

            return xml;
        }

        private static string GetValue(
            XElement? parent,
            string elementName)
        {
            if (parent == null)
                return "";

            return parent
                .Descendants()
                .FirstOrDefault(x =>
                    x.Name.LocalName.Equals(
                        elementName,
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                ?.Value
                ?.Trim() ?? "";
        }
    }
}
