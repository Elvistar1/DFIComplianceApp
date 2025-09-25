using SQLite;
using System;

namespace DFIComplianceApp.Models
{
    public class CompanyRenewalExtended : CompanyRenewal
    {
        public string CompanyName { get; set; }
        public string CompanyLocation { get; set; }
        public string RenewedBy { get; set; }
    }
}

