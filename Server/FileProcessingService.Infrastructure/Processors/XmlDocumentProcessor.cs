﻿using FileProcessingService.Application.Common.Abstract;
using FileProcessingService.Application.Common.Extensions;
using FileProcessingService.Application.Common.Interfaces.Processors;
using FileProcessingService.Application.ProcessedFileContent.Commands;
using FileProcessingService.Infrastructure.Extensions;
using MediatR;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;

namespace FileProcessingService.Infrastructure.Processors
{
    public class XmlDocumentProcessor : IXmlDocumentProcessor
    {
        private readonly ILogger<XmlDocumentProcessor> _logger;
        public Dictionary<string, int> MatchingElements { get; set; }
        private readonly ISender _sender;

        public XmlDocumentProcessor(ILogger<XmlDocumentProcessor> logger, ISender sender)
        {
            _logger = logger;
            MatchingElements = new Dictionary<string, int>();
            _sender = sender;
        }

        public async Task Process(Stream stream, string[] elements, string sessionId)
        {
            XmlReaderSettings settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Parse,
                Async = true
            };

            using XmlReader reader = XmlReader.Create(stream, settings);
            while (await reader.ReadAsync())
            {
                if (reader.NodeType == XmlNodeType.Element)
                {
                    if (!string.IsNullOrWhiteSpace(reader.Name) && elements.Contains(reader.Name))
                    {
                        string key = reader.Name.ToLowerInvariant();

                        if (MatchingElements.ContainsKey(key))
                        {
                            MatchingElements[key] = MatchingElements[key] + 1;
                        }
                        else
                        {
                            if (!MatchingElements.TryAdd(key, 1))
                            {
                                _logger.LogError("Error during add {0} in memory", key);
                            }
                        }

                        var innerText = await reader.ReadElementContentAsStringAsync();
                        string cleanedInnerText = innerText.Replace("\n", " ");
                        var duplicateStatistics = cleanedInnerText.FindDuplicates().ToDuplicateWordModel();

                        if (!string.IsNullOrEmpty(innerText))
                        {
                            await _sender.Send(new CreateProcessedFileContentCommand(sessionId, innerText, key, duplicateStatistics));
                        }
                        //SetStatusMessage(sessionId, $"{key} has found {foundElements[key]} times");
                    }
                }

            }
        }
    }
}