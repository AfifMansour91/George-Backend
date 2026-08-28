using System;
using Microsoft.Playwright;

namespace George.Services.Utils
{
    public interface IHtmlRenderService
    {
        Task<byte[]> RenderPdfAsync(string html, CancellationToken cancellationToken = default);
        /// <summary>Renders HTML to a standard A4 PDF (may span multiple pages). Used by A4 order printouts.</summary>
        Task<byte[]> RenderPdfA4Async(string html, CancellationToken cancellationToken = default);
        Task<byte[]> RenderPngAsync(string html, CancellationToken cancellationToken = default);
    }

    public class PlaywrightHtmlRenderService : IHtmlRenderService, IAsyncDisposable
    {
        /// <summary>72mm at 96 CSS px/in; viewport must match PDF page width or layout reflows and clips.</summary>
        private static readonly int ReceiptCssWidthPx = (int)Math.Round(72.0 * 96.0 / 25.4);

        /// <summary>A4 width (210mm) at 96 CSS px/in.</summary>
        private static readonly int A4CssWidthPx = (int)Math.Round(210.0 * 96.0 / 25.4);

        private readonly Task<IPlaywright> _playwrightTask;
        private readonly Task<IBrowser> _browserTask;

        public PlaywrightHtmlRenderService()
        {
            _playwrightTask = Microsoft.Playwright.Playwright.CreateAsync();
            _browserTask = InitBrowserAsync();
        }

        private async Task<IBrowser> InitBrowserAsync()
        {
            var playwright = await _playwrightTask;
            return await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = true
            });
        }
        public async Task<byte[]> RenderPdfAsync(string html, CancellationToken cancellationToken = default)
        {
            var browser = await _browserTask;

            var page = await browser.NewPageAsync(new BrowserNewPageOptions
            {
                ViewportSize = new ViewportSize
                {
                    Width = ReceiptCssWidthPx,
                    Height = 3600
                },
                DeviceScaleFactor = 2
            });

            try
            {
                var finalHtml = EnsureReceiptCss(html);

                await page.SetContentAsync(finalHtml, new PageSetContentOptions
                {
                    WaitUntil = WaitUntilState.Load
                });

                // Stabilize line breaks for Hebrew / long names before print layout metrics.
                await page.EvaluateAsync(
                    "async () => { try { if (document.fonts && document.fonts.ready) await document.fonts.ready; } catch (_) { } }")
                    .ConfigureAwait(false);

                await page.EmulateMediaAsync(new PageEmulateMediaOptions { Media = Media.Print });

                await Task.Delay(50, cancellationToken).ConfigureAwait(false);

                // One PDF page = full receipt height. Chromium otherwise splits tall content into multiple pages;
                // Sumatra + thermal drivers then shrink or clip (bad output) instead of one continuous roll.
                var heightCssPx = await page.EvaluateAsync<double>(
                        @"() => {
                            const de = document.documentElement;
                            const b = document.body;
                            return Math.max(
                                de.scrollHeight, de.offsetHeight, de.clientHeight,
                                b ? Math.max(b.scrollHeight, b.offsetHeight, b.clientHeight) : 0);
                        }")
                    .ConfigureAwait(false);

                var heightMm = Math.Ceiling(heightCssPx * 25.4 / 96.0) + 6;
                heightMm = Math.Max(heightMm, 48);
                heightMm = Math.Min(heightMm, 8000);

                return await page.PdfAsync(new PagePdfOptions
                {
                    Width = "72mm",
                    Height = $"{heightMm}mm",
                    PrintBackground = true,
                    PreferCSSPageSize = false,
                    Margin = new Margin
                    {
                        Top = "0mm",
                        Bottom = "0mm",
                        Left = "0mm",
                        Right = "0mm"
                    }
                });
            }
            finally
            {
                await page.CloseAsync();
            }
        }

        /// <summary>
        /// Standard A4 PDF (multi-page allowed) - for A4 order printouts (Site.VoucherPrintA4).
        /// Unlike the receipt path, no 72mm CSS is injected and the page keeps natural A4 pagination;
        /// the payload HTML carries its own A4 layout (@page size A4).
        /// </summary>
        public async Task<byte[]> RenderPdfA4Async(string html, CancellationToken cancellationToken = default)
        {
            var browser = await _browserTask;

            var page = await browser.NewPageAsync(new BrowserNewPageOptions
            {
                ViewportSize = new ViewportSize
                {
                    Width = A4CssWidthPx,
                    Height = 1400
                },
                DeviceScaleFactor = 2
            });

            try
            {
                await page.SetContentAsync(html, new PageSetContentOptions
                {
                    WaitUntil = WaitUntilState.Load
                });

                await page.EvaluateAsync(
                    "async () => { try { if (document.fonts && document.fonts.ready) await document.fonts.ready; } catch (_) { } }")
                    .ConfigureAwait(false);

                await page.EmulateMediaAsync(new PageEmulateMediaOptions { Media = Media.Print });

                await Task.Delay(50, cancellationToken).ConfigureAwait(false);

                return await page.PdfAsync(new PagePdfOptions
                {
                    Format = "A4",
                    PrintBackground = true,
                    PreferCSSPageSize = false,
                    Margin = new Margin
                    {
                        Top = "10mm",
                        Bottom = "10mm",
                        Left = "10mm",
                        Right = "10mm"
                    }
                });
            }
            finally
            {
                await page.CloseAsync();
            }
        }
        //public async Task<byte[]> RenderPdfAsync(string html, CancellationToken cancellationToken = default)
        //{
        //    var browser = await _browserTask;
        //    var page = await browser.NewPageAsync(new BrowserNewPageOptions
        //    {
        //        ViewportSize = new ViewportSize
        //        {
        //            Width = 420,
        //            Height = 1200
        //        }
        //    });

        //    try
        //    {
        //        await page.SetContentAsync(html, new PageSetContentOptions
        //        {
        //            WaitUntil = WaitUntilState.Load
        //        });

        //        // Optional if you want screen colors instead of print adjustments
        //        // await page.EmulateMediaAsync(new PageEmulateMediaOptions { Media = Media.Screen });

        //        return await page.PdfAsync(new PagePdfOptions
        //        {
        //            Width = "72mm",
        //            PrintBackground = true,
        //            Margin = new Margin
        //            {
        //                Top = "4mm",
        //                Bottom = "4mm",
        //                Left = "2mm",
        //                Right = "2mm"
        //            }
        //        });
        //    }
        //    finally
        //    {
        //        await page.CloseAsync();
        //    }
        //}

        //public async Task<byte[]> RenderPngAsync(string html, CancellationToken cancellationToken = default)
        //{
        //    var browser = await _browserTask;
        //    var page = await browser.NewPageAsync(new BrowserNewPageOptions
        //    {
        //        ViewportSize = new ViewportSize
        //        {
        //            Width = 420,
        //            Height = 1200
        //        }
        //    });

        //    try
        //    {
        //        await page.SetContentAsync(html, new PageSetContentOptions
        //        {
        //            WaitUntil = WaitUntilState.Load
        //        });

        //        return await page.ScreenshotAsync(new PageScreenshotOptions
        //        {
        //            FullPage = true,
        //            Type = ScreenshotType.Png
        //        });
        //    }
        //    finally
        //    {
        //        await page.CloseAsync();
        //    }
        //}

        public async Task<byte[]> RenderPngAsync(string html, CancellationToken cancellationToken = default)
        {
            var browser = await _browserTask;

            var page = await browser.NewPageAsync(new BrowserNewPageOptions
            {
                ViewportSize = new ViewportSize
                {
                    Width = ReceiptCssWidthPx,
                    Height = 2400
                },
                DeviceScaleFactor = 2
            });

            try
            {
                var finalHtml = EnsureReceiptCss(html);

                await page.SetContentAsync(finalHtml, new PageSetContentOptions
                {
                    WaitUntil = WaitUntilState.Load
                });

                return await page.ScreenshotAsync(new PageScreenshotOptions
                {
                    FullPage = true,
                    Type = ScreenshotType.Png,
                    Scale = ScreenshotScale.Device
                });
            }
            finally
            {
                await page.CloseAsync();
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_browserTask.IsCompletedSuccessfully)
                await _browserTask.Result.CloseAsync();

            if (_playwrightTask.IsCompletedSuccessfully)
                _playwrightTask.Result.Dispose();
        }


        private static string EnsureReceiptCss(string html)
        {
            // Voucher HTML: 72mm logical width; #voucher-root matches PaperWidthMm. Do not force body padding/min-width or it overrides the payload.
            const string extraCss = @"
<style>
@page {
    size: 72mm auto;
    margin: 0;
}

html {
    width: 72mm;
    max-width: 72mm !important;
    margin: 0 !important;
    padding: 0 !important;
    -webkit-print-color-adjust: exact !important;
    print-color-adjust: exact !important;
}

body {
    width: 72mm;
    max-width: 72mm !important;
    margin: 0 auto !important;
    padding: 0 !important;
    box-sizing: border-box !important;
    background: #fff !important;
    -webkit-print-color-adjust: exact !important;
    print-color-adjust: exact !important;
    word-wrap: break-word;
    overflow-wrap: anywhere;
}

html, body {
    overflow: visible !important;
    overflow-x: visible !important;
    overflow-y: visible !important;
}

#voucher-root {
    min-width: 0 !important;
    overflow: visible !important;
    overflow-wrap: anywhere !important;
    word-break: break-word !important;
    line-break: anywhere !important;
}

/* Any long label/value ? force wrap inside 72mm PDF; exclude replaced elements */
#voucher-root *:not(img):not(svg):not(canvas) {
    overflow-wrap: anywhere !important;
    word-break: break-word !important;
    line-break: anywhere !important;
    max-width: 100% !important;
    min-width: 0 !important;
}

.large, #voucher-root .large {
    overflow-wrap: anywhere !important;
    word-break: break-word !important;
    max-width: 100% !important;
    display: block !important;
}

img {
    max-width: 100% !important;
}

* {
    box-sizing: border-box;
}
</style>";

            if (html.Contains("</head>", StringComparison.OrdinalIgnoreCase))
                return html.Replace("</head>", extraCss + "</head>", StringComparison.OrdinalIgnoreCase);

            return "<head>" + extraCss + "</head>" + html;
        }



    }
}
