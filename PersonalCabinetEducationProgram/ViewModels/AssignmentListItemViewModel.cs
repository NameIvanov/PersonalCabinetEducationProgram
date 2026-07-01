namespace PersonalCabinetEducationProgram.ViewModels
{
    public class AssignmentListItemViewModel
    {
        public DateTime? AssignedAt { get; set; }
        public string UserFullName { get; set; } = string.Empty;
        public string AssignmentType { get; set; } = string.Empty;
        public string TargetName { get; set; } = string.Empty;
        public string AssignedByFullName { get; set; } = string.Empty;
    }
}
