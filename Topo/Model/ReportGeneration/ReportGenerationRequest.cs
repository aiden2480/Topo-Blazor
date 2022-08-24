﻿namespace Topo.Model.ReportGeneration
{
    public enum ReportType
    {
        MemberList
    }

    public enum OutputType
    {
        PDF,
        Excel
    }

    public class ReportGenerationRequest
    {
        public ReportType ReportType { get; set; } = ReportType.MemberList;
        public string GroupName { get; set; } = string.Empty;
        public string Section { get; set; } = string.Empty;
        public string UnitName { get; set; } = string.Empty;
        public string ReportData { get; set; } = string.Empty;
        public OutputType OutputType { get; set; } = OutputType.PDF;
    }
}
