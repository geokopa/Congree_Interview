﻿using Microsoft.AspNetCore.Mvc;

namespace FileProcessingService.API.Controllers
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/[controller]")]
    public class FilesController : ControllerBase
    {

    }
}
