﻿using System.Globalization;
using System.IO;
using Topo.Model.Members;
using Topo.Model.OAS;

namespace Topo.Services
{
    public interface IOASService
    {
        public Task<List<OASStageListModel>> GetOASStagesList();
        public Task<List<OASTemplate>> GetOASTemplate(string templateName);
        public Task<List<OASWorksheetAnswers>> GenerateOASWorksheetAnswers(string selectedUnitId, OASStageListModel selectedStage, bool hideCompletedMembers, List<OASTemplate> templateList);
        public Task<List<OASWorksheetAnswers>> GenerateOASWorksheetAnswersForMember(string selectedUnitId, OASStageListModel selectedStage, bool hideCompletedMembers, List<OASTemplate> templateList, string memberId);
        public Task<GetUnitAchievementsResultsModel> GetUnitAchievements(string unit, string stream, string branch, int stage);
    }

    public class OASService : IOASService
    {
        private readonly StorageService _storageService;
        private readonly ITerrainAPIService _terrainAPIService;
        private readonly IMembersService _membersService;

        public OASService(StorageService storageService, ITerrainAPIService terrainAPIService, IMembersService membersService)
        {
            _storageService = storageService;
            _terrainAPIService = terrainAPIService;
            _membersService = membersService;
        }

        public async Task<List<OASStageListModel>> GetOASStagesList()
        {
            if (_storageService.OASStages.Any())
                return _storageService.OASStages;
            var oasStageList = new List<OASStageListModel>();
            foreach (var stream in GetOASStreamList())
            {
                var stageList = await GetOASStageList(stream.Key);
                oasStageList.AddRange(stageList);
            }
            _storageService.OASStages = oasStageList;
            return oasStageList;
        }

        private Dictionary<string, string> GetOASStreamList()
        {
            var oasStreams = new Dictionary<string, string>()
            {
                {"bushcraft", "Bushcraft" },
                {"bushwalking", "Bushwalking" },
                {"camping", "Camping" },
                {"aquatics", "Aquatics" },
                {"boating", "Boating" },
                {"paddling", "Paddling" },
                {"alpine", "Alpine" },
                {"cycling", "Cycling" },
                {"vertical", "Vertical" }
            };

            return oasStreams;
        }
        private async Task<List<OASStageListModel>> GetOASStageList(string stream)
        {
            //var eventListModel = new EventListModel();
            var getEventResultModel = await _terrainAPIService.GetOASTreeAsync(stream);
            var streamTitle = getEventResultModel.title;
            var oasStageListModels = new List<OASStageListModel>();
            ProcessTree(streamTitle, getEventResultModel.tree, oasStageListModels);
            return oasStageListModels;
        }
        private void ProcessTree(string streamTitle, Tree treeNode, List<OASStageListModel> oasStageListModels)
        {
            oasStageListModels.Add(new OASStageListModel
            {
                Stream = streamTitle,
                Branch = treeNode.branch_id,
                StageTitle = treeNode.title,
                Stage = treeNode.stage,
                TemplateLink = treeNode.template_link,
                SelectListItemText = FormatStageText(streamTitle, treeNode.title, treeNode.stage)
            });
            if (treeNode.children != null)
            {
                foreach (var child in treeNode.children)
                {
                    ProcessChild(streamTitle, child, oasStageListModels);
                }
            }
        }
        private void ProcessChild(string streamTitle, Child childNode, List<OASStageListModel> oasStageListModels)
        {
            oasStageListModels.Add(new OASStageListModel
            {
                Stream = streamTitle,
                Branch = childNode.branch_id,
                StageTitle = childNode.title,
                Stage = childNode.stage,
                TemplateLink = childNode.template_link,
                SelectListItemText = FormatStageText(streamTitle, childNode.title, childNode.stage)
            });
            if (childNode.children != null)
            {
                foreach (var child in childNode.children)
                {
                    ProcessChild(streamTitle, child, oasStageListModels);
                }
            }
        }

        private string FormatStageText(string streamTitle, string nodeTitle, int nodeStage)
        {
            return streamTitle == nodeTitle ? $"{streamTitle} {nodeStage}" : $"{streamTitle} {nodeTitle} {nodeStage}";
        }

        public async Task<List<OASTemplate>> GetOASTemplate(string templateName)
        {
            var templateList = _storageService.OASTemplates
                .Where(t => t.TemplateName == templateName)
                .OrderBy(t => t.InputGroupSort)
                .ThenBy(t => t.Id)
                .ToList();
            var templateId = 0;
            if (templateList == null || templateList.Count == 0)
            {
                var templateTitle = "";
                var inputGroupTitle = "";
                var oasTemplateResult = await _terrainAPIService.GetOASTemplateAsync(templateName);
                foreach (var document in oasTemplateResult.document)
                {
                    templateTitle = document.title;
                    foreach (var inputGroup in document.input_groups)
                    {
                        inputGroupTitle = inputGroup.title;
                        foreach (var input in inputGroup.inputs.Where(i => i.id != "file_uploader"))
                        {
                            var oasTemplate = new OASTemplate
                            {
                                Id = templateId++,
                                TemplateName = templateName,
                                TemplateTitle = templateTitle,
                                InputGroup = inputGroupTitle,
                                InputGroupSort = GetInputGroupSort(inputGroupTitle),
                                InputId = input.id,
                                InputLabel = input.label
                            };
                            _storageService.OASTemplates.Add(oasTemplate);
                        }
                    }
                }

                templateList = _storageService.OASTemplates
                .Where(t => t.TemplateName == templateName)
                .OrderBy(t => t.InputGroupSort)
                .ThenBy(t => t.Id)
                .ToList();
            }

            return templateList;
        }

        private int GetInputGroupSort(string inputGroup)
        {
            if (inputGroup == "Plan>")
                return 1;
            if (inputGroup == "Do>")
                return 2;
            if (inputGroup == "Review>")
                return 3;
            return 4;
        }

        public async Task<List<OASWorksheetAnswers>> GenerateOASWorksheetAnswersForMember(string selectedUnitId, OASStageListModel selectedStage, bool hideCompletedMembers, List<OASTemplate> templateList, string memberId)
        {
            var members = await _membersService.GetMembersAsync(selectedUnitId);
            var member = members.Where(m => m.member_number == memberId).ToList();
            return await GenerateOASWorksheetAnswers(selectedUnitId, selectedStage, hideCompletedMembers, templateList, member);
        }

        public async Task<List<OASWorksheetAnswers>> GenerateOASWorksheetAnswers(string selectedUnitId, OASStageListModel selectedStage, bool hideCompletedMembers, List<OASTemplate> templateList)
        {
            var members = await _membersService.GetMembersAsync(selectedUnitId);
            return await GenerateOASWorksheetAnswers(selectedUnitId, selectedStage, hideCompletedMembers, templateList, members);
        }

        private async Task<List<OASWorksheetAnswers>> GenerateOASWorksheetAnswers(string selectedUnitId, OASStageListModel selectedStage, bool hideCompletedMembers, List<OASTemplate> templateList, List<MemberListModel> members)
        {
            var getUnitAchievementsResultsModel = await GetUnitAchievements(selectedUnitId, selectedStage.Stream.ToLower(), selectedStage.Branch, selectedStage.Stage);
            var sortedMemberList = members.Where(m => m.isAdultLeader == 0).OrderBy(m => m.first_name).ThenBy(m => m.last_name).ToList();
            var templateTitle = templateList.Count > 0 ? templateList[0].TemplateTitle : "";
            if (hideCompletedMembers)
                templateTitle += " (in progress)";

            var OASWorksheetAnswers = new List<OASWorksheetAnswers>();

            foreach (var item in templateList.OrderBy(t => t.InputGroupSort).ThenBy(t => t.Id))
            {
                foreach (var member in sortedMemberList)
                {
                    OASWorksheetAnswers oASWorksheetAnswers = new OASWorksheetAnswers()
                    {
                        TemplateTitle = templateTitle,
                        InputId = item.InputId,
                        InputTitle = item.InputGroup.Replace(">", ""),
                        InputLabel = item.InputLabel,
                        InputTitleSortIndex = item.InputGroupSort,
                        InputSortIndex = item.Id,
                        MemberId = member.id,
                        MemberName = $"{member.first_name} {member.last_name}",
                        MemberPatrol = member.patrol_name,
                        MemberAnswer = null,
                        Answered = false,
                        Awarded = false
                    };
                    OASWorksheetAnswers.Add(oASWorksheetAnswers);
                }
            }

            foreach (var memberAchievement in getUnitAchievementsResultsModel.results)
            {
                var verifiedAnswers = new List<KeyValuePair<string, string>>();
                if (memberAchievement.answers != null)
                    verifiedAnswers = memberAchievement.answers.Where(a => a.Key.EndsWith("_verifiedDate") || a.Key == "logbook_up_to_date").ToList();
                // In progress
                if (memberAchievement.status == "draft_review" || memberAchievement.status == "pending_review")
                {
                    if (verifiedAnswers.Any())
                    {
                        foreach (var answer in verifiedAnswers)
                        {
                            var worksheetAnswer = OASWorksheetAnswers
                                .Where(wa => wa.InputId == answer.Key.Replace("_verifiedDate", ""))
                                .Where(wa => wa.MemberId == memberAchievement.member_id)
                                .FirstOrDefault();
                            if (worksheetAnswer != null)
                            {
                                if (answer.Key == "logbook_up_to_date" && answer.Value == "true")
                                {
                                    worksheetAnswer.Answered = true;
                                }
                                else
                                {
                                    worksheetAnswer.Answered = true;
                                }
                            }
                        }
                    }
                }

                // Awarded
                if (memberAchievement.status == "awarded")
                {
                    if (hideCompletedMembers)
                    {
                        // Remove member answers from list
                        var worksheetAnswersToRemove = OASWorksheetAnswers
                            .Where(wa => wa.MemberId == memberAchievement.member_id)
                            .Select(wa => wa.MemberId)
                            .ToList();
                        OASWorksheetAnswers.RemoveAll(r => worksheetAnswersToRemove.Any(a => a == r.MemberId));
                        continue;
                    }

                    // Imported
                    if (!string.IsNullOrEmpty(memberAchievement.imported.date_awarded))
                    {
                        // Conver string date from yyyy-mm-dd format
                        var importedDate = DateTime.ParseExact(memberAchievement.imported.date_awarded, "yyyy-MM-dd", CultureInfo.InvariantCulture);
                        // Get all answers
                        var worksheetAnswers = OASWorksheetAnswers
                            .Where(wa => wa.MemberId == memberAchievement.member_id)
                            .ToList();
                        // Set answer date for each answer
                        foreach (var worksheetAnswer in worksheetAnswers)
                        {
                            if (worksheetAnswer != null)
                            {
                                worksheetAnswer.MemberAnswer = importedDate;
                                worksheetAnswer.Answered = true;
                                worksheetAnswer.Awarded = true;
                            }
                        }
                    }
                    // No answers
                    if (memberAchievement.answers == null || !memberAchievement.answers.Any() || !verifiedAnswers.Any())
                    {
                        // Get all answers
                        var worksheetAnswers = OASWorksheetAnswers
                            .Where(wa => wa.MemberId == memberAchievement.member_id)
                            .ToList();
                        // Set answer date for each answer
                        foreach (var worksheetAnswer in worksheetAnswers)
                        {
                            if (worksheetAnswer != null)
                            {
                                {
                                    worksheetAnswer.MemberAnswer = memberAchievement.status_updated;
                                    worksheetAnswer.Answered = true;
                                    worksheetAnswer.Awarded = true;
                                }
                            }
                        }
                    }
                    // Answers
                    if (verifiedAnswers.Any())
                    {
                        foreach (var answer in verifiedAnswers)
                        {
                            var worksheetAnswer = OASWorksheetAnswers
                                .Where(wa => wa.InputId == answer.Key.Replace("_verifiedDate", ""))
                                .Where(wa => wa.MemberId == memberAchievement.member_id)
                                .FirstOrDefault();
                            if (worksheetAnswer != null)
                            {
                                worksheetAnswer.MemberAnswer = memberAchievement.status_updated;
                                worksheetAnswer.Answered = true;
                                worksheetAnswer.Awarded = true;
                            }
                            //try
                            //    {
                            //        worksheetAnswer.MemberAnswer = ConvertAnswerDate(answer.Value, memberAchievement.status_updated);
                            //    }
                            //    catch (Exception ex)
                            //    {
                            //        worksheetAnswer.MemberAnswer = memberAchievement.status_updated;
                            //    }
                        }
                    }
                    // Set logbook up to date
                    var logbookUpToDate = OASWorksheetAnswers
                        .Where(wa => wa.InputId == "logbook_up_to_date")
                        .Where(wa => wa.MemberId == memberAchievement.member_id)
                        .Where(wa => wa.MemberAnswer == null)
                        .FirstOrDefault();
                    if (logbookUpToDate != null)
                    {
                        logbookUpToDate.MemberAnswer = memberAchievement.status_updated;
                        logbookUpToDate.Answered = true;
                        logbookUpToDate.Awarded = true;
                    }
                }
            }

            var sortedAnswers = OASWorksheetAnswers
                .OrderBy(owa => owa.InputTitleSortIndex)
                .ThenBy(owa => owa.InputSortIndex)
                .ToList();

            return sortedAnswers;
        }

        public async Task<GetUnitAchievementsResultsModel> GetUnitAchievements(string unit, string stream, string branch, int stage)
        {
            return await _terrainAPIService.GetUnitOASAchievements(unit, stream, branch, stage);
        }

        private DateTime ConvertAnswerDate(string answerValue, DateTime updatedDate)
        {
            // Question answer dates seem to be either AU or US format. WTF!
            // "south_magnetic_find_electronic_means_compass_west_directions_e7e4fc_verifiedDate": "18/11/2020",
            // "least_activities_improved_bushcraft_learnt_from_enjoyed_talked_a5973d_verifiedDate": "11/18/2020",
            DateTime answerDate;
            try
            {
                answerDate = DateTime.ParseExact(answerValue, "dd/MM/yyyy", CultureInfo.InvariantCulture); // Date in AU format
                if (answerDate > updatedDate)
                    // Answer date is after when the record was updated, so treat as a US date.
                    answerDate = DateTime.ParseExact(answerValue, "M/dd/yyyy", CultureInfo.InvariantCulture); // Date in US format
                return answerDate;
            }
            catch
            {
                answerDate = DateTime.ParseExact(answerValue, "M/dd/yyyy", CultureInfo.InvariantCulture); // Date in US format
                return answerDate;
            }
        }

    }
}
