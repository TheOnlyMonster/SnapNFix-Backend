﻿using Microsoft.EntityFrameworkCore;
using SnapNFix.Application.Utilities;
using SnapNFix.Domain.Entities;
using SnapNFix.Domain.Enums;
using SnapNFix.Application.Common.Interfaces;
using SnapNFix.Application.Interfaces;
using SnapNFix.Domain.Interfaces;

namespace SnapNFix.Infrastructure.Services.ReportService;

public class ReportService : IReportService
{
    private readonly IUnitOfWork _unitOfWork;

    public ReportService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<bool> AttachReportWithNearbyIssue(SnapReport report, CancellationToken cancellationToken)
    {
        var radiusInDegrees = Constants.NearbyIssueRadiusMeters / 111000.0;
        
        var nearbyIssue = await _unitOfWork.Repository<Issue>()
            .FindBy(i => i.Category == report.ReportCategory &&
                         i.Location.IsWithinDistance(report.Location, radiusInDegrees))
            .OrderBy(i => i.Location.Distance(report.Location))
            .FirstOrDefaultAsync(cancellationToken);

        if (nearbyIssue != null)
        {
            report.IssueId = nearbyIssue.Id;
            report.Issue = nearbyIssue;
            return true;
        }

        return false;
      
            
        
    }
    public async Task<Issue> CreateIssueWithReportAsync(SnapReport report, CancellationToken cancellationToken)
    {
        var newIssue = new Issue
            {
                Location = report.Location,
                Category = report.ReportCategory,
                Status = IssueStatus.Pending,
                Severity = Severity.NotSpecified,
                ImagePath = report.ImagePath,
                CreatedAt = DateTime.UtcNow,
                Road = report.Road,
                City = report.City,
                State = report.State,
                Country = report.Country
            };

            await _unitOfWork.Repository<Issue>().Add(newIssue);
            report.IssueId = newIssue.Id;
            report.Issue = newIssue;
            return newIssue;
            //save the report with the new issue in case error
    }
}
