using FileProcessingService.API.BackgroundServices;
using FileProcessingService.API.Models;
using FileProcessingService.Application.Common.Interfaces.Processors;
using FileProcessingService.Application.ProcessedFileContent.Queries;
using FileProcessingService.Application.StatusMessages.Queries;
using FileProcessingService.Domain.Entities;
using FileProcessingService.Infrastructure.Extensions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace FileProcessingService.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FilesController : Controller
    {
        private readonly ISender _sender;
        private readonly IBackgroundTaskQueue _backgroundTaskQueue;
        private readonly IXmlDocumentProcessor _documentProcessor;
        private readonly IConfiguration _configuration;

        public FilesController(IBackgroundTaskQueue backgroundTaskQueue, IXmlDocumentProcessor documentProcessor, ISender sender, IConfiguration configuration)
        {
            _backgroundTaskQueue = backgroundTaskQueue;
            _documentProcessor = documentProcessor;
            _sender = sender;
            _configuration = configuration;
        }

        /// <summary>
        /// Upload XML file for processing. 
        /// </summary>
        /// <param name="file">XML file for processing</param>
        /// <param name="model">Uniqueu Identifier SessionID and Elements e.g. (li;p;a)</param>
        /// <returns></returns>
        /// <remarks>
        /// Consumer can specify elements you need to extract in uploaded document and also retreive some statistical inforamtion about word duplicates, found elements, and etc.
        /// </remarks>
        [HttpPost]
        [DisableRequestSizeLimit]
        [ProducesResponseType((int)HttpStatusCode.BadRequest)]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        public async Task<IActionResult> Process(IFormFile file, [FromForm] FileUploadModel model)
        {
            if (file == null || !IsValidXmlFile(file))
                return BadRequest("Please specify files.");

            var fileBytes = await AsMemoryByteArray(file);
            _backgroundTaskQueue.QueueBackgroundWorkItem(async token =>
            {
                await _documentProcessor.Process(fileBytes, model.Elements.AsCleanedArray(), model.SessionId, token);
            });

            return Ok();
        }

        /// <summary>
        /// Returns statuses of file processing with specified SessionID
        /// </summary>
        /// <param name="sessionId">Process Uniqueue Identificator</param>
        /// <param name="token"></param>
        /// <returns>Returns list of status messages</returns>
        [HttpGet]
        [Route("status-info/{sessionId}")]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(IEnumerable<StatusMessage>))]
        public async Task<IActionResult> StatusInfo([Required] string sessionId, CancellationToken token)
        {
            string statusAfter = string.Empty;

            if (Request.Headers.TryGetValue("statusAfter", out var dateValue))
            {
                statusAfter = dateValue;
            }

            var returnData = await _sender.Send(new GetStatusMessageBySessionQuery(statusAfter, sessionId), token);

            bool completed = returnData.Any(x => x.Completed);
            SetRetryHeader(completed);

            if (!returnData.Any())
                return NotFound();

            return Ok(returnData);
        }

        /// <summary>
        /// Get processed files data
        /// </summary>
        /// <param name="sessionId">Process Uniqueue Identifier</param>
        /// <param name="token"></param>
        /// <returns>Returns list of processed files data</returns>
        /// <remarks>
        /// Data will include word duplication statistics and also elements found in parsed xml document.
        /// </remarks>
        [HttpGet]
        [Route("processed/{sessionId}")]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(IEnumerable<ProcessedFileContent>))]
        public async Task<IActionResult> Processed(string sessionId, CancellationToken token)
        {
            var processedFile = await _sender.Send(new GetProcessedFileContentQuery { SessionId = sessionId }, token);

            if (!processedFile.Any())
                return NotFound();

            return Ok(processedFile);
        }

        #region Private Methods
        private static bool IsValidXmlFile(IFormFile file)
        {
            return file.ContentType == "text/xml" || file.ContentType == "application/xml";
        }

        private static async Task<byte[]> AsMemoryByteArray(IFormFile file)
        {
            var stream = new MemoryStream();
            await file.CopyToAsync(stream);
            return stream.ToArray();
        }

        private void SetRetryHeader(bool completed)
        {
            Response.Headers.Add("Retry-After", _configuration.GetValue<int>("Headers:RetryAfter").ToString());
            Response.Headers.Add("Completed", completed.ToString());
        }
        #endregion
    }
}
