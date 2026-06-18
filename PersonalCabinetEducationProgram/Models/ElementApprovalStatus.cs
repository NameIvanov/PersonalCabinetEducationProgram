namespace PersonalCabinetEducationProgram.Models
{
    public static class ElementApprovalStatus
    {
        public const string NotUploaded = "";
        public const string Uploaded = "Загружено";
        public const string OnApproval = "На согласовании";
        public const string Approved = "Согласовано";
        public const string RevisionRequired = "На доработку";
        public const string Published = "Опубликовано на сайте";

        public static readonly string[] EditableByManager = [NotUploaded, Uploaded, OnApproval, RevisionRequired];
        public static readonly string[] ApproverCanApprove = [Uploaded, OnApproval, RevisionRequired];
        public static readonly string[] ApproverCanReject = [Uploaded, OnApproval];

        public static string Normalize(string? status)
        {
            if (string.IsNullOrWhiteSpace(status))
            {
                return NotUploaded;
            }

            return status.Trim() switch
            {
                "На рассмотрении" => OnApproval,
                "Отклонено" => RevisionRequired,
                _ => status.Trim()
            };
        }

        public static bool IsLockedForNonAdmin(string? status)
        {
            var normalized = Normalize(status);
            return normalized is Approved or Published;
        }
    }
}
