namespace CallFree.AI.Prompting
{
    public static class AnswerJudgementSchema
    {
        public const string Json = @"
{
  ""type"": ""object"",
  ""properties"": {
    ""transcript"": { ""type"": ""string"", ""description"": ""Clean Korean transcript or meaning-preserving normalization of the player's spoken answer."" },
    ""isAppropriate"": { ""type"": ""boolean"", ""description"": ""Whether the answer is relevant to the question and current scene."" },
    ""isCorrect"": { ""type"": ""boolean"", ""description"": ""Whether the answer satisfies the core rubric."" },
    ""score"": { ""type"": ""number"", ""minimum"": 0, ""maximum"": 1, ""description"": ""Overall score from 0.0 to 1.0."" },
    ""reason"": { ""type"": ""string"", ""description"": ""Short Korean reason suitable for player feedback or debug UI."" },
    ""matchedCriteria"": { ""type"": ""array"", ""items"": { ""type"": ""string"" }, ""description"": ""Rubric criteria satisfied by the answer."" },
    ""missingCriteria"": { ""type"": ""array"", ""items"": { ""type"": ""string"" }, ""description"": ""Rubric criteria still missing from the answer."" },
    ""nextState"": { ""type"": ""string"", ""enum"": [""pass"", ""partial"", ""fail"", ""retry""], ""description"": ""Game-state recommendation."" },
    ""confidence"": { ""type"": ""number"", ""minimum"": 0, ""maximum"": 1, ""description"": ""Confidence in the judgement from 0.0 to 1.0."" }
  },
  ""required"": [""transcript"", ""isAppropriate"", ""isCorrect"", ""score"", ""reason"", ""matchedCriteria"", ""missingCriteria"", ""nextState"", ""confidence""]
}";
    }
}
