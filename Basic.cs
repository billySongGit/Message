using Com.Clout2.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging; 
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Linq;
using System;
using System.Text.RegularExpressions;

namespace Com.Clout2.Api.Controllers
{
	/// <summary>
	/// Messages RESTful API Controller (ASP.NET Core 2.0)
	/// </summary>
	[DataContract]
	public abstract class MessageBaseApiController : Controller
	{
		/// <summary>
		/// Entity Framework DbContext for this controller
		/// </summary>
		protected ApiDataContext _dbContext;

		/// <summary>
		/// ILoggerFactory injected into this controller for logging
		/// </summary>
		protected readonly ILogger _logger; 

		/// <summary>
		/// Tokenizer injected into this controller for encryption
		/// </summary>
		protected Tokenator _tokenizer = new Tokenator();

		/// <summary>
		/// Constructor for Controller
		/// </summary>
		/// <param name="apiDataContext">API Data Context</param>
		/// <param name="logger">ILoggerFactory</param>
		public MessageBaseApiController(ApiDataContext apiDataContext, ILoggerFactory logger)
		{
			_logger = logger.CreateLogger<MessageBaseApiController>(); 
			_dbContext = apiDataContext;
			_dbContext.Database.EnsureCreated();
		}

    /// <summary>
    /// Ping to verify Messages Api Health
    /// </summary>
    /// <param name="pingText">Arbitrary text passed into the servie</param>
    /// <response code="200">Ping Text Reversed</response>
    [HttpGet]
    [Route("/message/ping")]
    [ProducesResponseType(typeof(string), 200)]
    public virtual IActionResult PingMessages([FromQuery]string pingText)
    {
        var charArray = pingText.ToCharArray();
        Array.Reverse(charArray);
        return new OkObjectResult(new string(charArray));
    }

		/// <summary>
		/// Add new Message to the datastore
		/// </summary>
		/// <param name="loginToken">The token for the user requesting this data. 
		///	 If provided, the system verifies the user rights to access the data</param>
		/// <param name="body">Message object that needs to be added to the datastore</param>
		/// <response code="200">successful operation</response>
		/// <response code="405">Invalid input</response>
		[HttpPost]
		[Route("/message")]
		[ProducesResponseType(typeof(Message), 200)]
		[ProducesResponseType(typeof(IDictionary<string, string>), 400)]
		public virtual IActionResult AddMessage(
			[FromQuery]string loginToken,
			[FromBody]Message body)
		{
			if (string.IsNullOrEmpty(loginToken)) return BadRequest("Login token is required"); 
			var loggedInUser = _tokenizer.ValidateToken(loginToken);
			if (loggedInUser != null)
			{
				if (body != null)
				{
					_dbContext.Messages.Add(body);
					_dbContext.SaveChanges();
				}
				if (body != null && body.Id != null) return new OkObjectResult(body);
				else return NotFound("Record was not added");
			}
			else return BadRequest("Invalid or expired login token");
		}

		/// <summary>
		/// Delete Message to the datastore
		/// </summary>
		/// <param name="loginToken">The token for the user requesting this data. 
		///	 If provided, the system verifies the user rights to access the data</param>
		/// <param name="id">Id of the object that needs to be deleted from the datastore</param>
		/// <response code="200">successful operation</response>
		/// <response code="405">Invalid input</response>
		[HttpDelete]
		[Route("/message")]
		[ProducesResponseType(typeof(Message), 200)]
		[ProducesResponseType(typeof(IDictionary<string, string>), 400)]
		public virtual IActionResult DeleteMessage(
			[FromQuery]string loginToken,
			[FromQuery]long? id)
		{
			if (string.IsNullOrEmpty(loginToken)) return BadRequest("Login token is required"); 
			var loggedInUser = _tokenizer.ValidateToken(loginToken); 
			if (loggedInUser != null) 
			{ 
				if (id == null || id < 1) return new NotFoundResult(); 
				var body = _dbContext.Messages.SingleOrDefault(m => m.Id == id); 
				_dbContext.Messages.Remove(body); 
				_dbContext.SaveChanges(); 
				body = _dbContext.Messages.SingleOrDefault(m => m.Id == id); 
				if (body == null)
				{ 
					return Ok();  
				}
				else return BadRequest("Unable to delete object"); 
			} 
			else return BadRequest("Invalid or expired login token");
		}

		/// <summary>
		/// Retrieve Message from the datastore by Id
		/// </summary>
		/// <param name="loginToken">The token for the user requesting this data. 
		///    If provided, the system verifies the user rights to access the data</param>
		/// <param name="id">Id of the object to retrieve from the datastore</param>
		/// <response code="200">successful operation</response>
		/// <response code="405">Invalid input</response>
		[HttpGet]
		[Route("/message")]
		[ProducesResponseType(typeof(Message), 200)]
		[ProducesResponseType(typeof(IDictionary<string, string>), 400)]
		public virtual IActionResult GetMessage(
			[FromQuery]string loginToken,
			[FromQuery]long? id)
		{
			if (string.IsNullOrEmpty(loginToken)) return BadRequest("Login token is required"); 
			var loggedInUser = _tokenizer.ValidateToken(loginToken); 
			if (loggedInUser != null) 
			{ 
				if (id == null || id < 1) return new OkObjectResult(default(Message)); 
				var body = _dbContext.Messages
					// Include Parents
					//.Include("MessageTemplate")
					//.Include("User")
					//.Include("Email")
					//.Include("Phone")
					//.Include("User")
					// Include Children - comment out any that return large data sets or circular references
					.SingleOrDefault(m => m.Id == id); 
				if (body == null) return NotFound(); 
				return new OkObjectResult(body); 
			} 
			else return BadRequest("Invalid or expired login token");
		}

		/// <summary>
		/// List Messages from the data store with a startsWith filter
		/// </summary>
		/// <param name="loginToken">The token for the user requesting this data.
		///	 If provided, the system verifies the user rights to access the data</param>
		/// <param name="startsWith">the beginning of the text to find</param>
		/// <param name="templateId">Filter on templateId (null or 0 to not filter)</param>
		/// <param name="fromUserId">Filter on fromUserId (null or 0 to not filter)</param>
		/// <param name="toEmailId">Filter on toEmailId (null or 0 to not filter)</param>
		/// <param name="toPhoneId">Filter on toPhoneId (null or 0 to not filter)</param>
		/// <param name="toUserId">Filter on toUserId (null or 0 to not filter)</param>
		/// <param name="isActive">Filter on isActive (null to not filter)</param>
		/// <param name="hasRead">Filter on hasRead (null to not filter)</param>
		/// <param name="pageSize">the page size of the result</param>
		/// <param name="pageNumber">the number of the page to retrieve, starting at 0</param>
		/// <response code="200">successful operation</response>
		/// <response code="405">Invalid input</response>
		[HttpGet]
		[Route("/message/list")]
		[ProducesResponseType(typeof(Message), 200)]
		[ProducesResponseType(typeof(IDictionary<string, string>), 400)]
		public virtual IActionResult ListMessages(
			[FromQuery]string loginToken,
			[FromQuery]long? templateId,
			[FromQuery]long? fromUserId,
			[FromQuery]long? toEmailId,
			[FromQuery]long? toPhoneId,
			[FromQuery]long? toUserId,
			[FromQuery]bool? isActive,
			[FromQuery]bool? hasRead,
			[FromQuery]string startsWith = "",
			[FromQuery]int pageSize = 100,
			[FromQuery]int pageNumber = 0)
		{
			if (string.IsNullOrEmpty(loginToken)) return BadRequest("Login token is required"); 
			var loggedInUser = _tokenizer.ValidateToken(loginToken); 
			if (loggedInUser != null) 
			{ 
				int skip = pageNumber; 
				int take = pageSize; 
				skip = skip * take; 
				var results = _dbContext.Messages.Where(b => b.Id > 0 
					&& (templateId == null || b.TemplateId.Equals(templateId))
					&& (fromUserId == null || b.FromUserId.Equals(fromUserId))
					&& (toEmailId == null || b.ToEmailId.Equals(toEmailId))
					&& (toPhoneId == null || b.ToPhoneId.Equals(toPhoneId))
					&& (toUserId == null || b.ToUserId.Equals(toUserId))
					&& (isActive == null || b.IsActive.Equals(isActive))
					&& (hasRead == null || b.HasRead.Equals(hasRead))
					&& (b.Id.ToString().Equals(startsWith) ||
						b.SendEmail.StartsWith(startsWith) ||
						b.SendText.StartsWith(startsWith) ||
						b.Fields.StartsWith(startsWith) 
						)
					).Skip(skip).Take(take)
                    // Include Parents
                    .Include("MessageTemplate")
                    // .Include("User")
                    // .Include("Email")
                    // .Include("Phone")
                    // .Include("User")
                    .ToList(); 
				if (results == null || results.Count < 1) return NotFound(); 
				return new OkObjectResult(results); 
			} 
			else return BadRequest("Invalid or expired login token"); 
		}

		/// <summary>
		/// Count List Messages results from the data store with a startsWith filter
		/// </summary>
		/// <param name="loginToken">The token for the user requesting this data.
		///	 If provided, the system verifies the user rights to access the data</param>
		/// <param name="startsWith">the beginning of the text to find</param>
		/// <param name="templateId">Filter on templateId (null or 0 to not filter)</param>
		/// <param name="fromUserId">Filter on fromUserId (null or 0 to not filter)</param>
		/// <param name="toEmailId">Filter on toEmailId (null or 0 to not filter)</param>
		/// <param name="toPhoneId">Filter on toPhoneId (null or 0 to not filter)</param>
		/// <param name="toUserId">Filter on toUserId (null or 0 to not filter)</param>
		/// <param name="isActive">Filter on isActive (null to not filter)</param>
		/// <param name="hasRead">Filter on hasRead (null to not filter)</param>
		/// <response code="200">successful operation</response>
		/// <response code="405">Invalid input</response>
		[HttpGet]
		[Route("/message/list/count")]
		[ProducesResponseType(typeof(Message), 200)]
		[ProducesResponseType(typeof(IDictionary<string, string>), 400)]
		public virtual IActionResult ListMessagesCount(
			[FromQuery]string loginToken,
			[FromQuery]long? templateId,
			[FromQuery]long? fromUserId,
			[FromQuery]long? toEmailId,
			[FromQuery]long? toPhoneId,
			[FromQuery]long? toUserId,
			[FromQuery]bool? isActive,
			[FromQuery]bool? hasRead,
			[FromQuery]string startsWith = "")
		{
			if (string.IsNullOrEmpty(loginToken)) return BadRequest("Login token is required"); 
			var loggedInUser = _tokenizer.ValidateToken(loginToken); 
			if (loggedInUser != null) 
			{ 
				var results = _dbContext.Messages.Where(b => b.Id > 0 
					&& (templateId == null || b.TemplateId.Equals(templateId))
					&& (fromUserId == null || b.FromUserId.Equals(fromUserId))
					&& (toEmailId == null || b.ToEmailId.Equals(toEmailId))
					&& (toPhoneId == null || b.ToPhoneId.Equals(toPhoneId))
					&& (toUserId == null || b.ToUserId.Equals(toUserId))
					&& (isActive == null || b.IsActive.Equals(isActive))
					&& (hasRead == null || b.HasRead.Equals(hasRead))
					&& (b.Id.ToString().Equals(startsWith) ||
						b.SendEmail.StartsWith(startsWith) ||
						b.SendText.StartsWith(startsWith) ||
						b.Fields.StartsWith(startsWith) 
						)
					).Take(10000).Count(); 
				if (results < 1) return NotFound(); 
				return new OkObjectResult(results); 
			} 
			else return BadRequest("Invalid or expired login token"); 
		}

		/// <summary>
		/// Search Messages from the datastore with a Contains filter
		/// </summary>
		/// <param name="loginToken">The token for the user requesting this data. 
		///	 If provided, the system verifies the user rights to access the data</param>
		/// <param name="templateId">Filter on templateId (null or 0 to not filter)</param>
		/// <param name="fromUserId">Filter on fromUserId (null or 0 to not filter)</param>
		/// <param name="toEmailId">Filter on toEmailId (null or 0 to not filter)</param>
		/// <param name="toPhoneId">Filter on toPhoneId (null or 0 to not filter)</param>
		/// <param name="toUserId">Filter on toUserId (null or 0 to not filter)</param>
		/// <param name="isActive">Filter on isActive (null to not filter)</param>
		/// <param name="hasRead">Filter on hasRead (null to not filter)</param>
		/// <param name="q">the text to search for</param>
		/// <param name="pageSize">the page size of the result</param>
		/// <param name="pageNumber">the number of the page to retrieve, starting at 0</param>
		/// <response code="200">successful operation</response>
		/// <response code="405">Invalid input</response>
		[HttpGet]
		[Route("/message/search")]
		[ProducesResponseType(typeof(Message), 200)]
		[ProducesResponseType(typeof(IDictionary<string, string>), 400)]
		public virtual IActionResult SearchMessages(
			[FromQuery]string loginToken,
			[FromQuery]long? templateId,
			[FromQuery]long? fromUserId,
			[FromQuery]long? toEmailId,
			[FromQuery]long? toPhoneId,
			[FromQuery]long? toUserId,
			[FromQuery]bool? isActive,
			[FromQuery]bool? hasRead,
			[FromQuery]string q = "",
			[FromQuery]int pageSize = 100,
			[FromQuery]int pageNumber = 0)
		{
			if (string.IsNullOrEmpty(loginToken)) return BadRequest("Login token is required"); 
			var loggedInUser = _tokenizer.ValidateToken(loginToken); 
			if (loggedInUser != null) 
			{ 
				int skip = pageNumber; 
				int take = pageSize; 
				skip = skip * take; 
				var results = _dbContext.Messages.Where(b => b.Id > 0 
					&& (templateId == null || b.TemplateId.Equals(templateId))
					&& (fromUserId == null || b.FromUserId.Equals(fromUserId))
					&& (toEmailId == null || b.ToEmailId.Equals(toEmailId))
					&& (toPhoneId == null || b.ToPhoneId.Equals(toPhoneId))
					&& (toUserId == null || b.ToUserId.Equals(toUserId))
					&& (isActive == null || b.IsActive.Equals(isActive))
					&& (hasRead == null || b.HasRead.Equals(hasRead))
					&& (b.Id.ToString().Equals(q) ||
						b.SendEmail.Contains(q) ||
						b.SendText.Contains(q) ||
						b.Fields.Contains(q) 
						)
					).Skip(skip).Take(take)
                    // Include Parents
                    .Include("MessageTemplate")
                    //.Include("User")
                    //.Include("Email")
                    //.Include("Phone")
                    //.Include("User")
                    .ToList(); 
				if (results == null || results.Count < 1) return NotFound(); 
				return new OkObjectResult(results); 
			} 
			else return BadRequest("Invalid or expired login token"); 
		}

		/// <summary>
		/// Count Search Messages results from the datastore with a Contains filter
		/// </summary>
		/// <param name="loginToken">The token for the user requesting this data. 
		///	 If provided, the system verifies the user rights to access the data</param>
		/// <param name="templateId">Filter on templateId (null or 0 to not filter)</param>
		/// <param name="fromUserId">Filter on fromUserId (null or 0 to not filter)</param>
		/// <param name="toEmailId">Filter on toEmailId (null or 0 to not filter)</param>
		/// <param name="toPhoneId">Filter on toPhoneId (null or 0 to not filter)</param>
		/// <param name="toUserId">Filter on toUserId (null or 0 to not filter)</param>
		/// <param name="isActive">Filter on isActive (null to not filter)</param>
		/// <param name="hasRead">Filter on hasRead (null to not filter)</param>
		/// <param name="q">the text to search for</param>
		/// <response code="200">successful operation</response>
		/// <response code="405">Invalid input</response>
		[HttpGet]
		[Route("/message/search/count")]
		[ProducesResponseType(typeof(Message), 200)]
		[ProducesResponseType(typeof(IDictionary<string, string>), 400)]
		public virtual IActionResult SearchMessagesCount(
			[FromQuery]string loginToken,
			[FromQuery]long? templateId,
			[FromQuery]long? fromUserId,
			[FromQuery]long? toEmailId,
			[FromQuery]long? toPhoneId,
			[FromQuery]long? toUserId,
			[FromQuery]bool? isActive,
			[FromQuery]bool? hasRead,
			[FromQuery]string q = "")
		{
			if (string.IsNullOrEmpty(loginToken)) return BadRequest("Login token is required"); 
			var loggedInUser = _tokenizer.ValidateToken(loginToken); 
			if (loggedInUser != null) 
			{ 
				var results = _dbContext.Messages.Where(b => b.Id > 0 
					&& (templateId == null || b.TemplateId.Equals(templateId))
					&& (fromUserId == null || b.FromUserId.Equals(fromUserId))
					&& (toEmailId == null || b.ToEmailId.Equals(toEmailId))
					&& (toPhoneId == null || b.ToPhoneId.Equals(toPhoneId))
					&& (toUserId == null || b.ToUserId.Equals(toUserId))
					&& (isActive == null || b.IsActive.Equals(isActive))
					&& (hasRead == null || b.HasRead.Equals(hasRead))
					&& (b.Id.ToString().Equals(q) ||
						b.SendEmail.Contains(q) ||
						b.SendText.Contains(q) ||
						b.Fields.Contains(q) 
						)
					).Take(10000).Count(); 
				if (results < 1) return NotFound(); 
				return new OkObjectResult(results); 
			} 
			else return BadRequest("Invalid or expired login token"); 
		}

		/// <summary>
		/// Select Messages from the datastore with a list of ids
		/// </summary>
		/// <param name="loginToken">The token for the user requesting this data. 
		///	 If provided, the system verifies the user rights to access the data</param>
		/// <param name="ids">A string list of Message Ids, separated by commas</param> 
		/// <response code="200">successful operation</response> 
		/// <response code="404">Category object not found</response> 
		[HttpGet]
		[Route("/message/select")]
		[ProducesResponseType(typeof(Message), 200)]
		[ProducesResponseType(typeof(IDictionary<string, string>), 400)]
		public virtual IActionResult SelectMessages(
			[FromQuery]string loginToken,
			[FromQuery]string ids) 
		{
			if (string.IsNullOrEmpty(loginToken)) return BadRequest("Login token is required"); 
			var loggedInUser = _tokenizer.ValidateToken(loginToken); 
			if (loggedInUser == null) return BadRequest("Invalid or expired Login Token"); 
			// prevent SQL injection 
			Regex digitsAndCommaOnly = new Regex(@"[^\d,]"); 
			var idList = digitsAndCommaOnly.Replace(ids, ""); 
			var results = _dbContext.Messages.FromSql("Select * from CloutApi.Messages where Messages.Id in (" + idList + "); ").ToList(); 
			if (results == null || results.Count < 1) return NotFound("No matching records found"); 
			return new OkObjectResult(results); 
		} 

		/// <summary>
		/// Update Message in the datastore
		/// </summary>
		/// <param name="loginToken">The token for the user requesting this data. 
		///	 If provided, the system verifies the user rights to access the data</param>
		/// <param name="body">Message object that needs to be updated in the datastore</param>
		/// <response code="200">successful operation</response>
		/// <response code="405">Invalid input</response>
		[HttpPut]
		[Route("/message")]
		[ProducesResponseType(typeof(Message), 200)]
		[ProducesResponseType(typeof(IDictionary<string, string>), 400)]
		public virtual IActionResult UpdateMessage(
			[FromQuery]string loginToken,
			[FromBody]Message body)
		{
			if (string.IsNullOrEmpty(loginToken)) return BadRequest("Login token is required"); 
			var loggedInUser = _tokenizer.ValidateToken(loginToken);
			if (loggedInUser != null)
			{
				var itemToUpdate = _dbContext.Messages.Single(b => b.Id == body.Id);
				if (itemToUpdate != null) 
				{ 
					if (body.TemplateId != null && !body.TemplateId.Equals(itemToUpdate.TemplateId)) 
						itemToUpdate.TemplateId = body.TemplateId; 
					if (body.FromUserId != null && !body.FromUserId.Equals(itemToUpdate.FromUserId)) 
						itemToUpdate.FromUserId = body.FromUserId; 
					if (body.ToUserId != null && !body.ToUserId.Equals(itemToUpdate.ToUserId)) 
						itemToUpdate.ToUserId = body.ToUserId; 
					if (body.ToEmailId != null && !body.ToEmailId.Equals(itemToUpdate.ToEmailId)) 
						itemToUpdate.ToEmailId = body.ToEmailId; 
					if (body.ToPhoneId != null && !body.ToPhoneId.Equals(itemToUpdate.ToPhoneId)) 
						itemToUpdate.ToPhoneId = body.ToPhoneId; 
					if (body.SendEmail != null && !body.SendEmail.Equals(itemToUpdate.SendEmail)) 
						itemToUpdate.SendEmail = body.SendEmail; 
					if (body.EmailSent != null && !body.EmailSent.Equals(itemToUpdate.EmailSent)) 
						itemToUpdate.EmailSent = body.EmailSent; 
					if (body.SendText != null && !body.SendText.Equals(itemToUpdate.SendText)) 
						itemToUpdate.SendText = body.SendText; 
					if (body.SmsSent != null && !body.SmsSent.Equals(itemToUpdate.SmsSent)) 
						itemToUpdate.SmsSent = body.SmsSent; 
					if (body.Fields != null && !body.Fields.Equals(itemToUpdate.Fields)) 
						itemToUpdate.Fields = body.Fields; 
					if (body.Requested != null && !body.Requested.Equals(itemToUpdate.Requested)) 
						itemToUpdate.Requested = body.Requested; 
					if (body.IsActive != null && !body.IsActive.Equals(itemToUpdate.IsActive)) 
						itemToUpdate.IsActive = body.IsActive; 
					if (body.HasRead != null && !body.HasRead.Equals(itemToUpdate.HasRead)) 
						itemToUpdate.HasRead = body.HasRead; 
					_dbContext.SaveChanges(); 
					return Ok("Successful operation, no data returned"); 
				} 
				else return NotFound("Message not found"); 
			} 
			else return BadRequest("Invalid or expired login token"); 
		}

	} // End of Class MessageApiController
} // End of Namespace Com.Clout2.Controllers
