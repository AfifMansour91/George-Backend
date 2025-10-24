using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using George.Data;
using George.Common;


namespace George.Services
{
    public class DataRefreshService : BackgroundService
    {
        //*********************  Data members/Constants  *********************//
        private const int WAIT_TIME = 15 * 1000; // 15 sec.
        private readonly ILogger<DataRefreshService> _logger;
        private readonly DataUpdater _dataUpdater;


        //**************************    Construction    **************************//
        public DataRefreshService(ILogger<DataRefreshService> logger, DataUpdater dataUpdater)
        {
            _logger = logger;
			_dataUpdater = dataUpdater;
        }


        //*************************    Public Methods    *************************//
		public override async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation($"**** {this.GetType().Name} START at: {DateTimeOffset.Now} ****");

            await base.StartAsync(cancellationToken);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                _logger.LogInformation($"**** {this.GetType().Name} EXECUTE at: {DateTimeOffset.Now} ****");

                PagingDto paging = new(SysConfig.Data.DefaultPageSize);
                while (!stoppingToken.IsCancellationRequested)
				{
					try
					{
						bool res = await _dataUpdater.UpdateNextAsync(paging, stoppingToken);

						paging.Skip += SysConfig.Data.DefaultPageSize;

						// Reset paging.
						if (!res)
							paging.Skip = 0;

						// Next cycle.
						await Task.Delay(SysConfig.Data.RefreshDataWaitTimeInMillisec, stoppingToken);

					}
					catch (Exception ex)
					{
                        _logger.LogError($"Failed to update next batch from GovMap - ex: {ex.ToString()}");

                        // Next cycle - wait long.
						await Task.Delay(SysConfig.Data.RefreshDataWaitTimeLongInMillisec, stoppingToken);
					}
				}
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in MessageSendService ExecuteAsync");
                throw;
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation($"**** {this.GetType().Name} STOP at: {DateTimeOffset.Now} ****");

			//await _dataUpdater.StopAsync(cancellationToken);

            await base.StopAsync(cancellationToken);
        }

        //public void EnqueueWork(string queueName, QueueMessage data)
        //{
        //    _GovilProvider.EnqueueWork(queueName, data);
        //}
    }
}