﻿using System;
using System.Threading.Tasks;
using Bit.Icons.Models;
using Bit.Icons.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace Bit.Icons.Controllers
{
    [Route("")]
    public class IconsController : Controller
    {
        private readonly IMemoryCache _memoryCache;
        private readonly IDomainMappingService _domainMappingService;
        private readonly IIconFetchingService _iconFetchingService;
        private readonly IconsSettings _iconsSettings;

        public IconsController(
            IMemoryCache memoryCache,
            IDomainMappingService domainMappingService,
            IIconFetchingService iconFetchingService,
            IconsSettings iconsSettings)
        {
            _memoryCache = memoryCache;
            _domainMappingService = domainMappingService;
            _iconFetchingService = iconFetchingService;
            _iconsSettings = iconsSettings;
        }

        [HttpGet("{hostname}/icon.png")]
        [ResponseCache(Duration = 604800 /*7 days*/)]
        public async Task<IActionResult> Get(string hostname)
        {
            if(string.IsNullOrWhiteSpace(hostname) || !hostname.Contains("."))
            {
                return new BadRequestResult();
            }

            var url = $"http://{hostname}";
            if(!Uri.TryCreate(url, UriKind.Absolute, out Uri uri))
            {
                return new BadRequestResult();
            }

            var mappedDomain = _domainMappingService.MapDomain(uri.Host);
            if(!_memoryCache.TryGetValue(mappedDomain, out Icon icon))
            {
                var result = await _iconFetchingService.GetIconAsync(mappedDomain);
                if(result == null)
                {
                    return new NotFoundResult();
                }

                icon = result.Icon;

                // Only cache smaller images (<= 50kb)
                if(icon.Image.Length <= 50012)
                {
                    _memoryCache.Set(mappedDomain, icon, new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = new TimeSpan(_iconsSettings.CacheHours, 0, 0)
                    });
                }
            }

            return new FileContentResult(icon.Image, icon.Format);
        }
    }
}
