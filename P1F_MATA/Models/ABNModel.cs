namespace P1F_MATA.Models
{
    public class ABNModel
    {
        public string date_find { get; set; } = string.Empty;
        public string facility { get; set; } = string.Empty;
        public string facility_id { get; set; } = string.Empty;
        public string line { get; set; } = string.Empty;
        public string station_id { get; set; } = string.Empty;
        public string tpm_tag { get; set; } = string.Empty;
        public string tag_id { get; set; } = string.Empty;
        public string operator_sesa { get; set; } = string.Empty;
        public string findings { get; set; } = string.Empty;
        public string picture { get; set; } = string.Empty;
        public string status_request { get; set; } = string.Empty;
        public string status_action { get; set; } = string.Empty;
        public string status_dynamic { get; set; } = string.Empty;
        public string name_owner { get; set; } = string.Empty;
        public string order_id { get; set; } = string.Empty;
        public string validator_sesa { get; set; } = string.Empty;
        public string name_validator { get; set; } = string.Empty;
        public string assigned_sesa { get; set; } = string.Empty;
        public string assigned_name { get; set; } = string.Empty;
        public string owner_sesa { get; set; } = string.Empty;
        public string requestor_sesa { get; set; } = string.Empty;
        public string fixed_by_type { get; set; } = string.Empty;
        public string abn_type { get; set; } = string.Empty;
        public string abn_type_id { get; set; } = string.Empty;
        public string abn_happen { get; set; } = string.Empty;
        public string abn_rootcause { get; set; } = string.Empty;
        public string abn_rootcause_id { get; set; } = string.Empty;
        public string rootcause_analysis { get; set; } = string.Empty;
        public string am_checklist { get; set; } = string.Empty;
        public string corrective { get; set; } = string.Empty;
        public string target_completion { get; set; } = string.Empty;
        public string completed_date { get; set; } = string.Empty;
        public string image { get; set; } = string.Empty;
        public string attachment_file { get; set; } = string.Empty;
        public string machine_part { get; set; } = string.Empty;
    }
    public class ABNModelHistory
    {
        public string order_id { get; set; } = string.Empty;
        public string sesa_id { get; set; } = string.Empty;
        public string name { get; set; } = string.Empty;
        public string ova { get; set; } = string.Empty;
        public string remark { get; set; } = string.Empty;
        public string record_date { get; set; } = string.Empty;
    }
}
