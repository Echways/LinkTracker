using LinkTracker.Scrapper.Application.Clients.StackOverflow.Contracts;

namespace LinkTracker.Scrapper.Application.Clients.StackOverflow;

public interface IStackOverflowClient
{
    Task<StackOverflowQuestionResponse?> GetQuestionAsync(long questionId, CancellationToken ct = default);

    Task<IReadOnlyList<StackOverflowAnswerResponse>> GetAnswersAsync(long questionId, CancellationToken ct = default);

    Task<IReadOnlyList<StackOverflowCommentResponse>> GetCommentsAsync(long questionId, CancellationToken ct = default);
}