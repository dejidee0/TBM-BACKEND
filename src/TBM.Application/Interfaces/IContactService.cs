using TBM.Application.DTOs.Common;
using TBM.Application.DTOs.Contact;

namespace TBM.Application.Interfaces;

public interface IContactService
{
    Task<ApiResponse<ContactSubmissionResultDto>> SubmitAsync(CreateContactMessageDto dto);
}
