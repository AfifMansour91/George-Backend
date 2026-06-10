namespace George.Services.Payments.Cardcom;

internal static class CardcomDocumentPayload
{
    public static Dictionary<string, object?> ToDictionary(CardcomTransactionDocument doc)
    {
        var products = doc.Products.Select(p =>
        {
            var line = new Dictionary<string, object?>
            {
                ["Description"] = p.Description,
                ["Quantity"] = p.Quantity,
                ["UnitCost"] = p.UnitCost,
            };
            if (!string.IsNullOrWhiteSpace(p.ProductId))
                line["ProductID"] = p.ProductId;
            if (p.TotalLineCost is > 0)
                line["TotalLineCost"] = p.TotalLineCost.Value;
            if (p.IsVatFree)
                line["IsVatFree"] = true;
            return line;
        }).ToList();

        var body = new Dictionary<string, object?>
        {
            ["DocumentTypeToCreate"] = doc.DocumentTypeToCreate,
            ["Name"] = doc.Name,
            ["IsSendByEmail"] = doc.SendByEmail,
            ["Language"] = doc.Language,
            ["ISOCoinID"] = doc.IsoCoinId,
            ["Products"] = products,
        };

        if (!string.IsNullOrWhiteSpace(doc.TaxId))
            body["TaxId"] = doc.TaxId;
        if (!string.IsNullOrWhiteSpace(doc.Email))
            body["Email"] = doc.Email;
        if (!string.IsNullOrWhiteSpace(doc.Phone))
            body["Phone"] = doc.Phone;
        if (!string.IsNullOrWhiteSpace(doc.Mobile))
            body["Mobile"] = doc.Mobile;
        if (doc.SendBySms)
            body["IsSendSMS"] = true;
        if (!string.IsNullOrWhiteSpace(doc.Comments))
            body["Comments"] = doc.Comments;
        if (!string.IsNullOrWhiteSpace(doc.ExternalId))
            body["ExternalId"] = doc.ExternalId;

        return body;
    }
}
