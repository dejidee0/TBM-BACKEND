using TBM.Application.DTOs.ProjectRequests;
using TBM.Core.Entities.ProjectRequests;
using TBM.Core.Interfaces.Repositories;

namespace TBM.Application.Services;

public class ProjectRequestService
{
    private readonly IProjectRequestRepository _projectRequests;

    public ProjectRequestService(IProjectRequestRepository projectRequests)
    {
        _projectRequests = projectRequests;
    }

    public async Task<ProjectRequestResponseDto> CreateAsync(string requestType, CreateProjectRequestDto dto)
    {
        ValidateRequest(dto);

        var entity = new ProjectRequest
        {
            EstimateId = dto.EstimateId,
            RequestType = requestType,
            ProjectDescription = string.IsNullOrWhiteSpace(dto.ProjectDescription) ? null : dto.ProjectDescription.Trim(),
            ContactName = dto.ContactName.Trim(),
            ContactPhone = dto.ContactPhone.Trim(),
            ContactEmail = dto.ContactEmail.Trim(),
            AdditionalNotes = string.IsNullOrWhiteSpace(dto.AdditionalNotes) ? null : dto.AdditionalNotes.Trim(),
            Status = "Received"
        };

        await _projectRequests.CreateAsync(entity);

        return new ProjectRequestResponseDto
        {
            Success = true,
            RequestId = entity.Id,
            RequestType = entity.RequestType,
            Status = entity.Status,
            Message = "Request received. Our team will contact you within 24 hours."
        };
    }

    private static void ValidateRequest(CreateProjectRequestDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.ContactName))
        {
            throw new ArgumentException("Contact name is required.");
        }

        if (string.IsNullOrWhiteSpace(dto.ContactPhone))
        {
            throw new ArgumentException("Contact phone is required.");
        }

        if (string.IsNullOrWhiteSpace(dto.ContactEmail))
        {
            throw new ArgumentException("Contact email is required.");
        }
    }
}
