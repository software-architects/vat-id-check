using System;

namespace VatIDChecker
{
    public class ValidationParams
    {
        public string valid { get; set; }
        public string name { get; set; }
        public string address { get; set; }
        public string cCode { get; set; }
        public string vatNum { get; set; }
    }

    public class Tax
    {
        public string name { get; set; }
        public string rate { get; set; }
        public string amount { get; set; }
        public string amount_plain { get; set; }
        public string amount_rounded { get; set; }
        public string amount_net { get; set; }
        public string amount_net_plain { get; set; }
        public string amount_net_rounded { get; set; }
        public string amount_gross { get; set; }
        public string amount_gross_plain { get; set; }
        public string amount_gross_rounded { get; set; }
    }

    public class Taxes
    {
        public Tax tax { get; set; }
    }

    public class Invoice
    {
        public string id { get; set; }
        public DateTime created { get; set; }
        public DateTime updated { get; set; }
        public string client_id { get; set; }
        public string contact_id { get; set; }
        public string invoice_number { get; set; }
        public string number { get; set; }
        public string number_pre { get; set; }
        public string number_length { get; set; }
        public string title { get; set; }
        public string date { get; set; }
        public string supply_date { get; set; }
        public string supply_date_type { get; set; }
        public string due_date { get; set; }
        public string due_days { get; set; }
        public string address { get; set; }
        public string status { get; set; }
        public string label { get; set; }
        public string intro { get; set; }
        public string note { get; set; }
        public string total_net { get; set; }
        public string total_gross { get; set; }
        public string reduction { get; set; }
        public string total_reduction { get; set; }
        public string total_net_unreduced { get; set; }
        public string total_gross_unreduced { get; set; }
        public string currency_code { get; set; }
        public string quote { get; set; }
        public string net_gross { get; set; }
        public string discount_rate { get; set; }
        public string discount_date { get; set; }
        public string discount_days { get; set; }
        public string discount_amount { get; set; }
        public string paid_amount { get; set; }
        public string open_amount { get; set; }
        public string payment_types { get; set; }
        public Taxes taxes { get; set; }
        public string invoice_id { get; set; }
        public string offer_id { get; set; }
        public string confirmation_id { get; set; }
        public string recurring_id { get; set; }
        public string dig_proceeded { get; set; }
        public string template_id { get; set; }
        public string customfield { get; set; }
    }

    public class InvoiceObject
    {
        public Invoice invoice { get; set; }
    }
    public class ClientObject
    {
        public Client client { get; set; }
    }
    public class Client
    {
        public string id { get; set; }
        public DateTime created { get; set; }
        public DateTime updated { get; set; }
        public string archived { get; set; }
        public string dig_exclude { get; set; }
        public string client_number { get; set; }
        public string number { get; set; }
        public string number_pre { get; set; }
        public string number_length { get; set; }
        public string name { get; set; }
        public string salutation { get; set; }
        public string first_name { get; set; }
        public string last_name { get; set; }
        public string street { get; set; }
        public string zip { get; set; }
        public string city { get; set; }
        public string state { get; set; }
        public string country_code { get; set; }
        public string address { get; set; }
        public string phone { get; set; }
        public string fax { get; set; }
        public string mobile { get; set; }
        public string email { get; set; }
        public string www { get; set; }
        public string tax_number { get; set; }
        public string vat_number { get; set; }
        public string bank_account_owner { get; set; }
        public string bank_number { get; set; }
        public string bank_name { get; set; }
        public string bank_account_number { get; set; }
        public string bank_swift { get; set; }
        public string bank_iban { get; set; }
        public string currency_code { get; set; }
        public string enable_customerportal { get; set; }
        public string default_payment_types { get; set; }
        public string sepa_mandate { get; set; }
        public string sepa_mandate_date { get; set; }
        public string locale { get; set; }
        public string tax_rule { get; set; }
        public string net_gross { get; set; }
        public string price_group { get; set; }
        public string debitor_account_number { get; set; }
        public string reduction { get; set; }
        public string discount_rate_type { get; set; }
        public string discount_rate { get; set; }
        public string discount_days_type { get; set; }
        public string discount_days { get; set; }
        public string due_days_type { get; set; }
        public string due_days { get; set; }
        public string reminder_due_days_type { get; set; }
        public string reminder_due_days { get; set; }
        public string offer_validity_days_type { get; set; }
        public string offer_validity_days { get; set; }
        public string dunning_run { get; set; }
        public string note { get; set; }
        public string revenue_gross { get; set; }
        public string revenue_net { get; set; }
        public string customfield { get; set; }
        public string client_property_values { get; set; }
    }
}
