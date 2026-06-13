using TBM.Core.Entities.DesignFlow;
using TBM.Core.Enums;
using TBM.Core.Interfaces;

namespace TBM.Application.Services.DesignFlow;

public class ProjectTimelineService
{
    private readonly IUnitOfWork _unitOfWork;

    public ProjectTimelineService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<List<ProjectTimeline>> GenerateTimelineFromBOMAsync(
        Project project,
        BillOfMaterials? bom,
        CancellationToken cancellationToken = default)
    {
        var startDate = project.StartDate == default
            ? DateTime.UtcNow.Date
            : project.StartDate.Date;

        var itemCount = bom?.ItemCount ?? 0;
        var bufferDays = itemCount >= 8 ? 5 : 3;

        var milestones = new[]
        {
            ("Project kickoff", "Confirm scope, design direction, and timeline.", 0),
            ("Material reservation", "Reserve inventory and validate BOM readiness.", bufferDays),
            ("Procurement", "Finalize remaining materials and supplier dispatch.", bufferDays + 4),
            ("Delivery to site", "Deliver materials to the project location.", bufferDays + 9),
            ("Installation", "Install materials and complete finishing works.", bufferDays + 16),
            ("Quality check", "Inspect workmanship and close punch list items.", bufferDays + 22)
        };

        var timelines = new List<ProjectTimeline>();
        for (var index = 0; index < milestones.Length; index++)
        {
            var milestone = milestones[index];
            var timeline = new ProjectTimeline
            {
                ProjectId = project.Id,
                MilestoneName = milestone.Item1,
                Description = milestone.Item2,
                PlannedDate = startDate.AddDays(milestone.Item3),
                Status = ProjectTimelineStatus.Pending,
                SortOrder = index + 1
            };

            await _unitOfWork.Projects.AddTimelineAsync(timeline);
            timelines.Add(timeline);
        }

        return timelines;
    }

    public async Task<ProjectTimeline?> UpdateMilestoneAsync(
        Guid projectId,
        Guid milestoneId,
        ProjectTimelineStatus status,
        DateTime? actualDate = null,
        string? description = null)
    {
        var project = await _unitOfWork.Projects.GetByIdAsync(projectId);
        var milestone = project?.Timelines.FirstOrDefault(x => x.Id == milestoneId);

        if (milestone == null)
        {
            return null;
        }

        milestone.Status = status;
        if (actualDate.HasValue)
        {
            milestone.ActualDate = actualDate;
        }

        if (!string.IsNullOrWhiteSpace(description))
        {
            milestone.Description = description.Trim();
        }

        await _unitOfWork.SaveChangesAsync();
        return milestone;
    }
}
