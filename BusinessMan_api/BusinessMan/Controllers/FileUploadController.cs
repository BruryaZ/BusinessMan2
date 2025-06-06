﻿using AutoMapper;
using BusinessMan.Core.DTO_s;
using BusinessMan.Core.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using Microsoft.AspNetCore.Http;
using System.IO;
using System.Threading.Tasks;
using Amazon.S3;
using BusinessMan.Core.BasicModels;

namespace BusinessMan.API.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class FileUploadController : Controller
    {
        private readonly IService<FileDto> _fileService;
        private readonly IMapper _mapper;
        private readonly IAmazonS3 _s3Client;
        private readonly string _bucketName;
        private readonly IConfiguration _configuration;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public FileUploadController(IService<FileDto> fileService, IMapper mapper, IAmazonS3 amazonS3, IConfiguration configuration, IHttpContextAccessor httpContextAccessor)
        {
            _fileService = fileService;
            _mapper = mapper;
            _s3Client = amazonS3;
            _configuration = configuration;
            _bucketName = configuration["AWS:BucketName"] ?? throw new ArgumentNullException("AWS:BucketName");
            _httpContextAccessor = httpContextAccessor;
        }

        // GET: api/<FileUploadController>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<FileDto>>> GetAsync()
        {
            var files = await _fileService.GetListAsync();
            var filesDto = _mapper.Map<IEnumerable<FileDto>>(files);
            return Ok(filesDto);
        }

        // GET api/<FileUploadController>/5
        [HttpGet("{id}")]
        public async Task<ActionResult<FileDto>> GetAsync(int id)
        {
            var file = await _fileService.GetByIdAsync(id);
            if (file == null)
                return NotFound();

            var fileDto = _mapper.Map<FileDto>(file);
            return Ok(fileDto);
        }

        // POST api/<FileUploadController>
        [HttpPost("upload")]
        public async Task<IActionResult> Upload(IFormFile fileUpload)
        {
            if (fileUpload == null || fileUpload.Length == 0)
                return BadRequest("לא נבחר קובץ.");

            if (fileUpload.Length > 50 * 1024 * 1024) // מגבלת 50MB
                return BadRequest("גודל הקובץ חורג מהמגבלה המותרת.");

            // בדיקת סוג קובץ מותר
            var allowedExtensions = new[] { ".jpg", ".png", ".pdf", ".docx", ".txt" };
            var fileExtension = Path.GetExtension(fileUpload.FileName).ToLower();
            if (!allowedExtensions.Contains(fileExtension))
                return BadRequest("סוג הקובץ אינו נתמך.");

            var fileName = Path.GetFileNameWithoutExtension(fileUpload.FileName);
            var key = $"uploads/{Guid.NewGuid()}_{fileName}{fileExtension}";

            try
            {
                using var stream = fileUpload.OpenReadStream();

                var uploadRequest = new Amazon.S3.Transfer.TransferUtilityUploadRequest
                {
                    InputStream = stream,
                    BucketName = _bucketName,
                    Key = key,
                    ContentType = fileUpload.ContentType,
                    CannedACL = S3CannedACL.BucketOwnerFullControl
                };

                var transferUtility = new Amazon.S3.Transfer.TransferUtility(_s3Client);
                await transferUtility.UploadAsync(uploadRequest);

                string fileUrl = $"https://{_bucketName}.s3.{_s3Client.Config.RegionEndpoint.SystemName}.amazonaws.com/{key}";

                var user = _httpContextAccessor.HttpContext.Items["CurrentUser"] as User;

                var fileDto = new FileDto
                {
                    FileName = $"{fileName}{fileExtension}",
                    Size = fileUpload.Length,
                    UploadDate = DateTime.UtcNow,
                    FilePath = fileUrl,
                    BusinessId = user.BusinessId ?? 0,
                    FileContent = fileUpload,
                    UserId = user.Id,
                };

                var createdFile = await _fileService.AddAsync(fileDto);

                return Ok(new { message = "ההעלאה הצליחה", fileUrl });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"אירעה שגיאה בהעלאת הקובץ: {ex.Message}");
            }
        }

        // PUT api/<FileUploadController>/5
        [HttpPut("{id}")]
        public async Task<ActionResult<FileDto>> PutAsync(int id, [FromForm] FileDto fileUpload)
        {
            var updatedFile = await _fileService.UpdateAsync(id, fileUpload);
            if (updatedFile == null)
            {
                return NotFound();
            }

            // שמירת הקובץ במערכת (למשל, במערכת הקבצים)
            var filePath = Path.Combine("path_to_storage", fileUpload.FileName);
            // TODO: Save file at AWS S3.
            return Ok(updatedFile);
        }

        // DELETE api/<FileUploadController>/5
        [HttpDelete("{id}")]
        public async Task<ActionResult> DeleteAsync(int id)
        {
            var file = await _fileService.GetByIdAsync(id);
            if (file is null)
            {
                return NotFound();
            }
            await _fileService.DeleteAsync(file);

            // מחיקת הקובץ מהמערכת (למשל, ממערכת הקבצים)
            var filePath = Path.Combine("path_to_storage", file.FileName);
            // TODO: Save file to AWS S3.

            return NoContent();
        }

        // הורדת כל הקבצים של המשתמש לזיפ
        [HttpGet("my-files-download-zip")]
        public async Task<IActionResult> DownloadAllMyFilesAsZip()
        {
            var businessIdClaim = User?.FindFirst("business_id")?.Value;
            if (string.IsNullOrEmpty(businessIdClaim))
                return Unauthorized("העסק לא מזוהה.");

            int businessId = int.Parse(businessIdClaim);

            var allFiles = await _fileService.GetListAsync();
            var myFiles = allFiles.Where(f => f.BusinessId == businessId);

            using var memoryStream = new MemoryStream();
            using (var archive = new System.IO.Compression.ZipArchive(memoryStream, System.IO.Compression.ZipArchiveMode.Create, true))
            {
                foreach (var file in myFiles)
                {
                    var request = new Amazon.S3.Model.GetObjectRequest
                    {
                        BucketName = _bucketName,
                        Key = ExtractKeyFromUrl(file.FilePath)
                    };

                    using var response = await _s3Client.GetObjectAsync(request);
                    using var responseStream = response.ResponseStream;
                    var entry = archive.CreateEntry(file.FileName, System.IO.Compression.CompressionLevel.Fastest);

                    using var entryStream = entry.Open();
                    await responseStream.CopyToAsync(entryStream);
                }
            }

            memoryStream.Position = 0;
            return File(memoryStream.ToArray(), "application/zip", "my-files.zip");
        }

        // GET api/FileUpload/download-file/{id}
        [HttpGet("download-file/{id}")]
        public async Task<IActionResult> DownloadFile(int id)
        {
            var file = await _fileService.GetByIdAsync(id);
            if (file == null)
                return NotFound("הקובץ לא נמצא.");

            // הוצאת המפתח (Key) מתוך כתובת ה-URL של הקובץ ב-S3
            var key = ExtractKeyFromUrl(file.FilePath);

            try
            {
                var request = new Amazon.S3.Model.GetObjectRequest
                {
                    BucketName = _bucketName,
                    Key = key
                };

                using var response = await _s3Client.GetObjectAsync(request);
                using var responseStream = response.ResponseStream;

                // קריאה לזרם התגובה לזיכרון
                using var memoryStream = new MemoryStream();
                await responseStream.CopyToAsync(memoryStream);
                memoryStream.Position = 0;

                // החזרת הקובץ עם סוג התוכן המקורי והשם לשמירה
                return File(memoryStream.ToArray(), response.Headers.ContentType, file.FileName);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"שגיאה בהורדת הקובץ: {ex.Message}");
            }
        }

        [HttpGet("my-files")]
        public async Task<IActionResult> GetMyFiles()
        {
            var businessIdClaim = User?.FindFirst("business_id")?.Value;
            if (string.IsNullOrEmpty(businessIdClaim))
                return Unauthorized("העסק לא מזוהה.");
            int businessId = int.Parse(businessIdClaim);
            var allFiles = await _fileService.GetListAsync();
            var myFiles = allFiles.Where(f => f.BusinessId == businessId);
            return Ok(myFiles);
        }
        private string ExtractKeyFromUrl(string url)
        {
            // מניח שה־Key מתחיל אחרי amazonaws.com/
            var uri = new Uri(url);
            return uri.AbsolutePath.TrimStart('/');
        }
    }
}
