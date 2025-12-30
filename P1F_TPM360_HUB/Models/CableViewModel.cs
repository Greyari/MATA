namespace P1F_TPM360_HUB.Models
{
    public class CableViewModel
    {
        public string CableId { get; set; }
        public string CablePart { get; set; }
        public string CableDescription { get; set; }
        public string Status { get; set; }
        public string LocationGroup { get; set; }
        public string UnitModel { get; set; }
    }
    public class DrawerCapacity
    {
        public string Line { get; set; }
        public string Location { get; set; }
        public int MaxQty { get; set; }
    }
    public class CableLocationResult
    {
        public string Line { get; set; }
        public string Location { get; set; }
    }
}
