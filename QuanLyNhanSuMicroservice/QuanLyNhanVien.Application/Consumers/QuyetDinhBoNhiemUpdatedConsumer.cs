using MassTransit;
using MediatR;
using QuyetDinhService.QuyetDinhService.Application.Events;
using QuanLyNhanSuMicroservice.QuanLyNhanVien.Application.Command.NhanSu;
using System.Threading.Tasks;

namespace QuanLyNhanSuMicroservice.QuanLyNhanVien.Application.Consumers
{
    public class QuyetDinhBoNhiemUpdatedConsumer(IMediator mediator) : IConsumer<QuyetDinhBoNhiemUpdatedEvent>
    {
        public async Task Consume(ConsumeContext<QuyetDinhBoNhiemUpdatedEvent> context)
        {
            var message = context.Message;

            // Update employee's position using the existing MediatR handler
            await mediator.Send(new UpdateNhanVienBoNhiemDTO(
                message.MaNhanVien,
                message.ChucVuMoi,
                message.PhuCapMoi
            ));
        }
    }
}
