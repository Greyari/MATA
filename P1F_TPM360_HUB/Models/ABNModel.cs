namespace P1F_TPM360_HUB.Models
{
    public class ABNModel
    {
        public string date_find { get; set; }
        public string facility { get; set; }
        public string facility_id { get; set; }

        public string line { get; set; }
        public string station_id { get; set; }
        public string tpm_tag { get; set; }
        public string tag_id { get; set; }
        public string operator_sesa { get; set; }
        public string findings { get; set; }
        public string picture { get; set; }
        public string status_request { get; set; }
        public string status_action { get; set; }
        public string status_dynamic { get; set; }
        public string name_owner { get; set; }
        public string order_id { get; set; }
        public string validator_sesa { get; set; }
        public string name_validator { get; set; }
        public string assigned_sesa { get; set; }
        public string assigned_name { get; set; }
        public string owner_sesa { get; set; }
        public string requestor_sesa { get; set; }
        public string fixed_by_type { get; set; }
        public string abn_type { get; set; }
        public string abn_type_id { get; set; }
        public string abn_happen { get; set; }
        public string abn_rootcause { get; set; }
        public string abn_rootcause_id { get; set; }
        public string rootcause_analysis { get; set; }
        public string am_checklist { get; set; }
        public string corrective { get; set; }
        public string target_completion { get; set; }
        public string completed_date { get; set; }
        public string image { get; set; }
        public string attachment_file { get; set; }
        public string machine_part { get; set; }

    }

    public class ABNModelHistory
    {
        public string order_id { get; set; }
        public string sesa_id { get; set; }
        public string name { get; set; }
        public string ova { get; set; }
        public string remark { get; set; }
        public string record_date { get; set; }
    }
}
