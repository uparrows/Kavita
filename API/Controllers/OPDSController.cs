using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Serialization;
using API.Comparators;
using API.Constants;
using API.DTOs;
using API.DTOs.Filtering;
using API.DTOs.OPDS;
using API.Entities;
using API.Extensions;
using API.Helpers;
using API.Interfaces;
using API.Interfaces.Services;
using API.Services;
using Kavita.Common;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    public class OpdsController : BaseApiController
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IDownloadService _downloadService;
        private readonly IDirectoryService _directoryService;
        private readonly UserManager<AppUser> _userManager;
        private readonly ICacheService _cacheService;
        private readonly IReaderService _readerService;


        private readonly XmlSerializer _xmlSerializer;
        private readonly XmlSerializer _xmlOpenSearchSerializer;
        private const string Prefix = "/api/opds/";
        private readonly FilterDto _filterDto = new FilterDto()
        {
            MangaFormat = null
        };
        private readonly ChapterSortComparer _chapterSortComparer = new ChapterSortComparer();

        public OpdsController(IUnitOfWork unitOfWork, IDownloadService downloadService,
            IDirectoryService directoryService, UserManager<AppUser> userManager,
            ICacheService cacheService, IReaderService readerService)
        {
            _unitOfWork = unitOfWork;
            _downloadService = downloadService;
            _directoryService = directoryService;
            _userManager = userManager;
            _cacheService = cacheService;
            _readerService = readerService;


            _xmlSerializer = new XmlSerializer(typeof(Feed));
            _xmlOpenSearchSerializer = new XmlSerializer(typeof(OpenSearchDescription));

        }

        [HttpPost("{apiKey}")]
        [HttpGet("{apiKey}")]
        [Produces("application/xml")]
        public async Task<IActionResult> Get(string apiKey)
        {
            if (!(await _unitOfWork.SettingsRepository.GetSettingsDtoAsync()).EnableOpds)
                return BadRequest("此服务器上未启用 OPDS");
            var feed = CreateFeed("Kavita", string.Empty, apiKey);
            feed.Id = "root";
            feed.Entries.Add(new FeedEntry()
            {
                Id = "inProgress",
                Title = "进行中",
                Content = new FeedEntryContent()
                {
                    Text = "按进行中的浏览"
                },
                Links = new List<FeedLink>()
                {
                    CreateLink(FeedLinkRelation.SubSection, FeedLinkType.AtomNavigation, Prefix + $"{apiKey}/in-progress"),
                }
            });
            feed.Entries.Add(new FeedEntry()
            {
                Id = "recentlyAdded",
                Title = "最近添加",
                Content = new FeedEntryContent()
                {
                    Text = "按最近添加的浏览"
                },
                Links = new List<FeedLink>()
                {
                    CreateLink(FeedLinkRelation.SubSection, FeedLinkType.AtomNavigation, Prefix + $"{apiKey}/recently-added"),
                }
            });
            feed.Entries.Add(new FeedEntry()
            {
                Id = "allLibraries",
                Title = "所有书库",
                Content = new FeedEntryContent()
                {
                    Text = "按书库浏览"
                },
                Links = new List<FeedLink>()
                {
                    CreateLink(FeedLinkRelation.SubSection, FeedLinkType.AtomNavigation, Prefix + $"{apiKey}/libraries"),
                }
            });
            feed.Entries.Add(new FeedEntry()
            {
                Id = "allCollections",
                Title = "所有收藏",
                Content = new FeedEntryContent()
                {
                    Text = "按收藏浏览"
                },
                Links = new List<FeedLink>()
                {
                    CreateLink(FeedLinkRelation.SubSection, FeedLinkType.AtomNavigation, Prefix + $"{apiKey}/collections"),
                }
            });
            return CreateXmlResult(SerializeXml(feed));
        }


        [HttpGet("{apiKey}/libraries")]
        [Produces("application/xml")]
        public async Task<IActionResult> GetLibraries(string apiKey)
        {
            if (!(await _unitOfWork.SettingsRepository.GetSettingsDtoAsync()).EnableOpds)
                return BadRequest("此服务器上未启用 OPDS");
            var user = await GetUser(apiKey);
            var libraries = await _unitOfWork.LibraryRepository.GetLibrariesForUserIdAsync(user.Id);

            var feed = CreateFeed("所有书库", $"{apiKey}/libraries", apiKey);

            foreach (var library in libraries)
            {
                feed.Entries.Add(new FeedEntry()
                {
                    Id = library.Id.ToString(),
                    Title = library.Name,
                    Links = new List<FeedLink>()
                    {
                        CreateLink(FeedLinkRelation.SubSection, FeedLinkType.AtomNavigation, Prefix + $"{apiKey}/libraries/{library.Id}"),
                    }
                });
            }

            return CreateXmlResult(SerializeXml(feed));
        }

        [HttpGet("{apiKey}/collections")]
        [Produces("application/xml")]
        public async Task<IActionResult> GetCollections(string apiKey)
        {
            if (!(await _unitOfWork.SettingsRepository.GetSettingsDtoAsync()).EnableOpds)
                return BadRequest("此服务器上未启用 OPDS");
            var user = await GetUser(apiKey);
            var isAdmin = await _userManager.IsInRoleAsync(user, PolicyConstants.AdminRole);

            IEnumerable <CollectionTagDto> tags;
            if (isAdmin)
            {
                tags = await _unitOfWork.CollectionTagRepository.GetAllTagDtosAsync();
            }
            else
            {
                tags = await _unitOfWork.CollectionTagRepository.GetAllPromotedTagDtosAsync();
            }


            var feed = CreateFeed("所有收藏", $"{apiKey}/collections", apiKey);

            foreach (var tag in tags)
            {
                feed.Entries.Add(new FeedEntry()
                {
                    Id = tag.Id.ToString(),
                    Title = tag.Title,
                    Summary = tag.Summary,
                    Links = new List<FeedLink>()
                    {
                        CreateLink(FeedLinkRelation.SubSection, FeedLinkType.AtomNavigation, Prefix + $"{apiKey}/collections/{tag.Id}"),
                        CreateLink(FeedLinkRelation.Image, FeedLinkType.Image, $"/api/image/collection-cover?collectionId={tag.Id}"),
                        CreateLink(FeedLinkRelation.Thumbnail, FeedLinkType.Image, $"/api/image/collection-cover?collectionId={tag.Id}")
                    }
                });
            }

            return CreateXmlResult(SerializeXml(feed));
        }

        [HttpGet("{apiKey}/collections/{collectionId}")]
        [Produces("application/xml")]
        public async Task<IActionResult> GetCollection(int collectionId, string apiKey, [FromQuery] int pageNumber = 0)
        {
            if (!(await _unitOfWork.SettingsRepository.GetSettingsDtoAsync()).EnableOpds)
                return BadRequest("此服务器上未启用 OPDS");
            var user = await GetUser(apiKey);
            var isAdmin = await _userManager.IsInRoleAsync(user, PolicyConstants.AdminRole);

            IEnumerable <CollectionTagDto> tags;
            if (isAdmin)
            {
                tags = await _unitOfWork.CollectionTagRepository.GetAllTagDtosAsync();
            }
            else
            {
                tags = await _unitOfWork.CollectionTagRepository.GetAllPromotedTagDtosAsync();
            }

            var tag = tags.SingleOrDefault(t => t.Id == collectionId);
            if (tag == null)
            {
                return BadRequest("收藏集不存在或您无权访问");
            }

            var series = await _unitOfWork.SeriesRepository.GetSeriesDtoForCollectionAsync(collectionId, user.Id, new UserParams()
            {
                PageNumber = pageNumber,
                PageSize = 20
            });

            var feed = CreateFeed(tag.Title + " 收藏", $"{apiKey}/collections/{collectionId}", apiKey);
            AddPagination(feed, series, $"{Prefix}{apiKey}/collections/{collectionId}");

            foreach (var seriesDto in series)
            {
                feed.Entries.Add(CreateSeries(seriesDto, apiKey));
            }


            return CreateXmlResult(SerializeXml(feed));
        }

        [HttpGet("{apiKey}/libraries/{libraryId}")]
        [Produces("application/xml")]
        public async Task<IActionResult> GetSeriesForLibrary(int libraryId, string apiKey, [FromQuery] int pageNumber = 0)
        {
            if (!(await _unitOfWork.SettingsRepository.GetSettingsDtoAsync()).EnableOpds)
                return BadRequest("此服务器上未启用 OPDS");
            var user = await GetUser(apiKey);
            var library =
                (await _unitOfWork.LibraryRepository.GetLibrariesForUserIdAsync(user.Id)).SingleOrDefault(l =>
                    l.Id == libraryId);
            if (library == null)
            {
                return BadRequest("用户无权访问此库");
            }

            var series = await _unitOfWork.SeriesRepository.GetSeriesDtoForLibraryIdAsync(libraryId, user.Id, new UserParams()
            {
                PageNumber = pageNumber,
                PageSize = 20
            }, _filterDto);

            var feed = CreateFeed(library.Name, $"{apiKey}/libraries/{libraryId}", apiKey);
            AddPagination(feed, series, $"{Prefix}{apiKey}/libraries/{libraryId}");

            foreach (var seriesDto in series)
            {
                feed.Entries.Add(CreateSeries(seriesDto, apiKey));
            }

            return CreateXmlResult(SerializeXml(feed));
        }

        [HttpGet("{apiKey}/recently-added")]
        [Produces("application/xml")]
        public async Task<IActionResult> GetRecentlyAdded(string apiKey, [FromQuery] int pageNumber = 1)
        {
            if (!(await _unitOfWork.SettingsRepository.GetSettingsDtoAsync()).EnableOpds)
                return BadRequest("此服务器上未启用 OPDS");
            var user = await GetUser(apiKey);
            var recentlyAdded = await _unitOfWork.SeriesRepository.GetRecentlyAdded(0, user.Id, new UserParams()
            {
                PageNumber = pageNumber,
                PageSize = 20
            }, _filterDto);

            var feed = CreateFeed("最近添加", $"{apiKey}/recently-added", apiKey);
            AddPagination(feed, recentlyAdded, $"{Prefix}{apiKey}/recently-added");

            foreach (var seriesDto in recentlyAdded)
            {
                feed.Entries.Add(CreateSeries(seriesDto, apiKey));
            }


            return CreateXmlResult(SerializeXml(feed));
        }

        [HttpGet("{apiKey}/in-progress")]
        [Produces("application/xml")]
        public async Task<IActionResult> GetInProgress(string apiKey, [FromQuery] int pageNumber = 1)
        {
            if (!(await _unitOfWork.SettingsRepository.GetSettingsDtoAsync()).EnableOpds)
                return BadRequest("此服务器上未启用 OPDS");
            var user = await GetUser(apiKey);
            var userParams = new UserParams()
            {
                PageNumber = pageNumber,
                PageSize = 20
            };
            var results = await _unitOfWork.SeriesRepository.GetInProgress(user.Id, 0, userParams, _filterDto);
            var listResults = results.DistinctBy(s => s.Name).Skip((userParams.PageNumber - 1) * userParams.PageSize)
                .Take(userParams.PageSize).ToList();
            var pagedList = new PagedList<SeriesDto>(listResults, listResults.Count, userParams.PageNumber, userParams.PageSize);

            Response.AddPaginationHeader(pagedList.CurrentPage, pagedList.PageSize, pagedList.TotalCount, pagedList.TotalPages);

            var feed = CreateFeed("进行中", $"{apiKey}/in-progress", apiKey);
            AddPagination(feed, pagedList, $"{Prefix}{apiKey}/in-progress");

            foreach (var seriesDto in pagedList)
            {
                feed.Entries.Add(CreateSeries(seriesDto, apiKey));
            }

            return CreateXmlResult(SerializeXml(feed));
        }

        [HttpGet("{apiKey}/series")]
        [Produces("application/xml")]
        public async Task<IActionResult> SearchSeries(string apiKey, [FromQuery] string query)
        {
            if (!(await _unitOfWork.SettingsRepository.GetSettingsDtoAsync()).EnableOpds)
                return BadRequest("此服务器上未启用 OPDS");
            var user = await GetUser(apiKey);
            if (string.IsNullOrEmpty(query))
            {
                return BadRequest("您必须传递查询参数");
            }
            query = query.Replace(@"%", "");
            // Get libraries user has access to
            var libraries = (await _unitOfWork.LibraryRepository.GetLibrariesForUserIdAsync(user.Id)).ToList();

            if (!libraries.Any()) return BadRequest("用户无权访问任何库");

            var series = await _unitOfWork.SeriesRepository.SearchSeries(libraries.Select(l => l.Id).ToArray(), query);

            var feed = CreateFeed(query, $"{apiKey}/series?query=" + query, apiKey);

            foreach (var seriesDto in series)
            {
                feed.Entries.Add(CreateSeries(seriesDto, apiKey));
            }

            return CreateXmlResult(SerializeXml(feed));
        }

        [HttpGet("{apiKey}/search")]
        [Produces("application/xml")]
        public async Task<IActionResult> GetSearchDescriptor(string apiKey)
        {
            if (!(await _unitOfWork.SettingsRepository.GetSettingsDtoAsync()).EnableOpds)
                return BadRequest("此服务器上未启用 OPDS");
            var feed = new OpenSearchDescription()
            {
                ShortName = "搜索",
                Description = "搜索系列",
                Url = new SearchLink()
                {
                    Type = FeedLinkType.AtomAcquisition,
                    Template = $"{Prefix}{apiKey}/series?query=" + "{searchTerms}"
                }
            };

            await using var sm = new StringWriter();
            _xmlOpenSearchSerializer.Serialize(sm, feed);

            return CreateXmlResult(sm.ToString().Replace("utf-16", "utf-8"));
        }

        [HttpGet("{apiKey}/series/{seriesId}")]
        [Produces("application/xml")]
        public async Task<IActionResult> GetSeries(string apiKey, int seriesId)
        {
            if (!(await _unitOfWork.SettingsRepository.GetSettingsDtoAsync()).EnableOpds)
                return BadRequest("此服务器上未启用 OPDS");
            var user = await GetUser(apiKey);
            var series = await _unitOfWork.SeriesRepository.GetSeriesDtoByIdAsync(seriesId, user.Id);
            var volumes = await _unitOfWork.SeriesRepository.GetVolumesDtoAsync(seriesId, user.Id);
            var feed = CreateFeed(series.Name + " - 卷", $"{apiKey}/series/{series.Id}", apiKey);
            feed.Links.Add(CreateLink(FeedLinkRelation.Image, FeedLinkType.Image, $"/api/image/series-cover?seriesId={seriesId}"));
            foreach (var volumeDto in volumes)
            {
                feed.Entries.Add(CreateVolume(volumeDto, seriesId, apiKey));
            }

            return CreateXmlResult(SerializeXml(feed));
        }

        [HttpGet("{apiKey}/series/{seriesId}/volume/{volumeId}")]
        [Produces("application/xml")]
        public async Task<IActionResult> GetVolume(string apiKey, int seriesId, int volumeId)
        {
            if (!(await _unitOfWork.SettingsRepository.GetSettingsDtoAsync()).EnableOpds)
                return BadRequest("此服务器上未启用 OPDS");
            var user = await GetUser(apiKey);
            var series = await _unitOfWork.SeriesRepository.GetSeriesDtoByIdAsync(seriesId, user.Id);
            var volume = await _unitOfWork.SeriesRepository.GetVolumeAsync(volumeId);
            var chapters =
                (await _unitOfWork.VolumeRepository.GetChaptersAsync(volumeId)).OrderBy(x => double.Parse(x.Number),
                    _chapterSortComparer);

            var feed = CreateFeed(series.Name + " - 卷 " + volume.Name + " - 章 ", $"{apiKey}/series/{seriesId}/volume/{volumeId}", apiKey);
            foreach (var chapter in chapters)
            {
                feed.Entries.Add(new FeedEntry()
                {
                    Id = chapter.Id.ToString(),
                    Title = "章 " + chapter.Number,
                    Links = new List<FeedLink>()
                    {
                        CreateLink(FeedLinkRelation.SubSection, FeedLinkType.AtomNavigation, Prefix + $"{apiKey}/series/{seriesId}/volume/{volumeId}/chapter/{chapter.Id}"),
                        CreateLink(FeedLinkRelation.Image, FeedLinkType.Image, $"/api/image/chapter-cover?chapterId={chapter.Id}")
                    }
                });
            }

            return CreateXmlResult(SerializeXml(feed));
        }

        [HttpGet("{apiKey}/series/{seriesId}/volume/{volumeId}/chapter/{chapterId}")]
        [Produces("application/xml")]
        public async Task<IActionResult> GetChapter(string apiKey, int seriesId, int volumeId, int chapterId)
        {
            if (!(await _unitOfWork.SettingsRepository.GetSettingsDtoAsync()).EnableOpds)
                return BadRequest("此服务器上未启用 OPDS");
            var user = await GetUser(apiKey);
            var series = await _unitOfWork.SeriesRepository.GetSeriesDtoByIdAsync(seriesId, user.Id);
            var volume = await _unitOfWork.SeriesRepository.GetVolumeAsync(volumeId);
            var chapter = await _unitOfWork.VolumeRepository.GetChapterDtoAsync(chapterId);
            var files = await _unitOfWork.VolumeRepository.GetFilesForChapterAsync(chapterId);

            var feed = CreateFeed(series.Name + " - 卷 " + volume.Name + " - 章 ", $"{apiKey}/series/{seriesId}/volume/{volumeId}/chapter/{chapterId}", apiKey);
            foreach (var mangaFile in files)
            {
                feed.Entries.Add(CreateChapter(seriesId, volumeId, chapterId, mangaFile, series, volume, chapter, apiKey));
            }

            return CreateXmlResult(SerializeXml(feed));
        }

        /// <summary>
        /// Downloads a file
        /// </summary>
        /// <param name="seriesId"></param>
        /// <param name="volumeId"></param>
        /// <param name="chapterId"></param>
        /// <param name="filename">Not used. Only for Chunky to allow download links</param>
        /// <returns></returns>
        [HttpGet("{apiKey}/series/{seriesId}/volume/{volumeId}/chapter/{chapterId}/download/{filename}")]
        public async Task<ActionResult> DownloadFile(string apiKey, int seriesId, int volumeId, int chapterId, string filename)
        {
            if (!(await _unitOfWork.SettingsRepository.GetSettingsDtoAsync()).EnableOpds)
                return BadRequest("此服务器上未启用 OPDS");
            var files = await _unitOfWork.VolumeRepository.GetFilesForChapterAsync(chapterId);
            var (bytes, contentType, fileDownloadName) = await _downloadService.GetFirstFileDownload(files);
            return File(bytes, contentType, fileDownloadName);
        }

        private static ContentResult CreateXmlResult(string xml)
        {
            return new ContentResult
            {
                ContentType = "application/xml",
                Content = xml,
                StatusCode = 200
            };
        }

        private static void AddPagination(Feed feed, PagedList<SeriesDto> list, string href)
        {
            var url = href;
            if (href.Contains("?"))
            {
                url += "&amp;";
            }
            else
            {
                url += "?";
            }

            var pageNumber = Math.Max(list.CurrentPage, 1);

            if (pageNumber > 1)
            {
                feed.Links.Add(CreateLink(FeedLinkRelation.Prev, FeedLinkType.AtomNavigation, url + "pageNumber=" + (pageNumber - 1)));
            }

            if (pageNumber + 1 < list.TotalPages)
            {
                feed.Links.Add(CreateLink(FeedLinkRelation.Next, FeedLinkType.AtomNavigation, url + "pageNumber=" + (pageNumber + 1)));
            }

            // Update self to point to current page
            var selfLink = feed.Links.SingleOrDefault(l => l.Rel == FeedLinkRelation.Self);
            if (selfLink != null)
            {
                selfLink.Href = url + "pageNumber=" + pageNumber;
            }


            feed.Total = list.TotalPages * list.PageSize;
            feed.ItemsPerPage = list.PageSize;
            feed.StartIndex = (Math.Max(list.CurrentPage - 1, 0) * list.PageSize) + 1;
        }

        private static FeedEntry CreateSeries(SeriesDto seriesDto, string apiKey)
        {
            return new FeedEntry()
            {
                Id = seriesDto.Id.ToString(),
                Title = $"{seriesDto.Name} ({seriesDto.Format})",
                Summary = seriesDto.Summary,
                Links = new List<FeedLink>()
                {
                    CreateLink(FeedLinkRelation.SubSection, FeedLinkType.AtomNavigation, Prefix + $"{apiKey}/series/{seriesDto.Id}"),
                    CreateLink(FeedLinkRelation.Image, FeedLinkType.Image, $"/api/image/series-cover?seriesId={seriesDto.Id}"),
                    CreateLink(FeedLinkRelation.Thumbnail, FeedLinkType.Image, $"/api/image/series-cover?seriesId={seriesDto.Id}")
                }
            };
        }

        private static FeedEntry CreateSeries(SearchResultDto searchResultDto, string apiKey)
        {
            return new FeedEntry()
            {
                Id = searchResultDto.SeriesId.ToString(),
                Title = $"{searchResultDto.Name} ({searchResultDto.Format})",
                Links = new List<FeedLink>()
                {
                    CreateLink(FeedLinkRelation.SubSection, FeedLinkType.AtomNavigation, Prefix + $"{apiKey}/series/{searchResultDto.SeriesId}"),
                    CreateLink(FeedLinkRelation.Image, FeedLinkType.Image, $"/api/image/series-cover?seriesId={searchResultDto.SeriesId}"),
                    CreateLink(FeedLinkRelation.Thumbnail, FeedLinkType.Image, $"/api/image/series-cover?seriesId={searchResultDto.SeriesId}")
                }
            };
        }

        private static FeedEntry CreateVolume(VolumeDto volumeDto, int seriesId, string apiKey)
        {
            return new FeedEntry()
            {
                Id = volumeDto.Id.ToString(),
                Title = volumeDto.IsSpecial ? "特刊" : "卷 " + volumeDto.Name,
                Links = new List<FeedLink>()
                {
                    CreateLink(FeedLinkRelation.SubSection, FeedLinkType.AtomNavigation, Prefix + $"{apiKey}/series/{seriesId}/volume/{volumeDto.Id}"),
                    CreateLink(FeedLinkRelation.Image, FeedLinkType.Image, $"/api/image/volume-cover?volumeId={volumeDto.Id}"),
                    CreateLink(FeedLinkRelation.Thumbnail, FeedLinkType.Image, $"/api/image/volume-cover?volumeId={volumeDto.Id}")
                }
            };
        }

        private FeedEntry CreateChapter(int seriesId, int volumeId, int chapterId, MangaFile mangaFile, SeriesDto series, Volume volume, ChapterDto chapter, string apiKey)
        {
            var fileSize =
                DirectoryService.GetHumanReadableBytes(DirectoryService.GetTotalSize(new List<string>()
                    {mangaFile.FilePath}));
            var fileType = _downloadService.GetContentTypeFromFile(mangaFile.FilePath);
            var filename = Uri.EscapeUriString(Path.GetFileName(mangaFile.FilePath) ?? string.Empty);
            return new FeedEntry()
            {
                Id = mangaFile.Id.ToString(),
                Title = $"{series.Name} - 卷 {volume.Name} - 章 {chapter.Number}",
                Extent = fileSize,
                Summary = $"{fileType.Split("/")[1]} - {fileSize}",
                Format = mangaFile.Format.ToString(),
                Links = new List<FeedLink>()
                {
                    CreateLink(FeedLinkRelation.Image, FeedLinkType.Image, $"/api/image/chapter-cover?chapterId={chapterId}"),
                    CreateLink(FeedLinkRelation.Thumbnail, FeedLinkType.Image, $"/api/image/chapter-cover?chapterId={chapterId}"),
                    // Chunky requires a file at the end. Our API ignores this
                    CreateLink(FeedLinkRelation.Acquisition, fileType, $"{Prefix}{apiKey}/series/{seriesId}/volume/{volumeId}/chapter/{chapterId}/download/{filename}"),
                    CreatePageStreamLink(seriesId, volumeId, chapterId, mangaFile, apiKey)
                },
                Content = new FeedEntryContent()
                {
                    Text = fileType,
                    Type = "text"
                }
            };
        }

        [HttpGet("{apiKey}/image")]
        public async Task<ActionResult> GetPageStreamedImage(string apiKey, [FromQuery] int seriesId, [FromQuery] int volumeId,[FromQuery] int chapterId, [FromQuery] int pageNumber)
        {
            if (pageNumber < 0) return BadRequest("页数不能小于 0");
            var chapter = await _cacheService.Ensure(chapterId);
            if (chapter == null) return BadRequest("查找要读取的图像文件时出现问题");

            try
            {
                var (path, _) = await _cacheService.GetCachedPagePath(chapter, pageNumber);
                if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path)) return BadRequest($"没有该图像页面 {pageNumber}");

                var content = await _directoryService.ReadFileAsync(path);
                var format = Path.GetExtension(path).Replace(".", "");

                // Calculates SHA1 Hash for byte[]
                Response.AddCacheHeader(content);

                // Save progress for the user
                await _readerService.SaveReadingProgress(new ProgressDto()
                {
                    ChapterId = chapterId,
                    PageNum = pageNumber,
                    SeriesId = seriesId,
                    VolumeId = volumeId
                }, await GetUser(apiKey));

                return File(content, "image/" + format);
            }
            catch (Exception)
            {
                _cacheService.CleanupChapters(new []{ chapterId });
                throw;
            }
        }

        [HttpGet("{apiKey}/favicon")]
        public async Task<ActionResult> GetFavicon(string apiKey)
        {
            var files = _directoryService.GetFilesWithExtension(Path.Join(Directory.GetCurrentDirectory(), ".."), @"\.ico");
            if (files.Length == 0) return BadRequest("找不到图标");
            var path = files[0];
            var content = await _directoryService.ReadFileAsync(path);
            var format = Path.GetExtension(path).Replace(".", "");

            // Calculates SHA1 Hash for byte[]
            Response.AddCacheHeader(content);

            return File(content, "image/" + format);
        }

        /// <summary>
        /// Gets the user from the API key
        /// </summary>
        /// <returns></returns>
        private async Task<AppUser> GetUser(string apiKey)
        {
            var user = await _unitOfWork.UserRepository.GetUserByApiKeyAsync(apiKey);
            if (user == null)
            {
                throw new KavitaException("用户不存在");
            }

            return user;
        }

        private static FeedLink CreatePageStreamLink(int seriesId, int volumeId, int chapterId, MangaFile mangaFile, string apiKey)
        {
            var link = CreateLink(FeedLinkRelation.Stream, "image/jpeg", $"{Prefix}{apiKey}/image?seriesId={seriesId}&volumeId={volumeId}&chapterId={chapterId}&pageNumber=" + "{pageNumber}");
            link.TotalPages = mangaFile.Pages;
            return link;
        }

        private static FeedLink CreateLink(string rel, string type, string href)
        {
            return new FeedLink()
            {
                Rel = rel,
                Href = href,
                Type = type
            };
        }

        private static Feed CreateFeed(string title, string href, string apiKey)
        {
            var link = CreateLink(FeedLinkRelation.Self, string.IsNullOrEmpty(href) ?
                FeedLinkType.AtomNavigation :
                FeedLinkType.AtomAcquisition, Prefix + href);

            return new Feed()
            {
                Title = title,
                Icon = Prefix + $"{apiKey}/favicon",
                Links = new List<FeedLink>()
                {
                    link,
                    CreateLink(FeedLinkRelation.Start, FeedLinkType.AtomNavigation, Prefix + apiKey),
                    CreateLink(FeedLinkRelation.Search, FeedLinkType.AtomSearch, Prefix + $"{apiKey}/search")
                },
            };
        }

        private string SerializeXml(Feed feed)
        {
            if (feed == null) return string.Empty;
            using var sm = new StringWriter();
            _xmlSerializer.Serialize(sm, feed);
            return sm.ToString().Replace("utf-16", "utf-8"); // Chunky cannot accept UTF-16 feeds
        }
    }
}
