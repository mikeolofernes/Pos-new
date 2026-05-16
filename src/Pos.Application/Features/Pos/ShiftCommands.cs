using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Pos.Application.Common.Abstractions;
using Pos.BuildingBlocks;
using Pos.Domain.Sales;
using Pos.Domain.Tenancy;

namespace Pos.Application.Features.Pos;

public sealed record OpenShiftCommand(Guid ShopId, Guid RegisterId, decimal OpeningFloat) : IRequest<Result<Guid>>;

public class OpenShiftValidator : AbstractValidator<OpenShiftCommand>
{
    public OpenShiftValidator()
    {
        RuleFor(x => x.ShopId).NotEmpty();
        RuleFor(x => x.RegisterId).NotEmpty();
        RuleFor(x => x.OpeningFloat).GreaterThanOrEqualTo(0);
    }
}

public class OpenShiftHandler : IRequestHandler<OpenShiftCommand, Result<Guid>>
{
    private readonly IAppDbContext _db;
    private readonly ITenantContext _tenant;
    public OpenShiftHandler(IAppDbContext db, ITenantContext t) { _db = db; _tenant = t; }

    public async Task<Result<Guid>> Handle(OpenShiftCommand req, CancellationToken ct)
    {
        var alreadyOpen = await _db.Shifts.AnyAsync(s =>
            s.RegisterId == req.RegisterId && s.Status == ShiftStatus.Open, ct);
        if (alreadyOpen) return Error.Conflict("shift.open.exists", "Register already has an open shift");

        var shift = new Shift
        {
            ShopId = req.ShopId,
            RegisterId = req.RegisterId,
            OpenedByUserId = _tenant.UserId ?? Guid.Empty,
            OpeningFloat = req.OpeningFloat,
            Status = ShiftStatus.Open,
            OpenedAt = DateTimeOffset.UtcNow
        };
        _db.Shifts.Add(shift);
        await _db.SaveChangesAsync(ct);
        return shift.Id;
    }
}

public sealed record CloseShiftCommand(Guid ShiftId, decimal ClosingDeclared) : IRequest<Result<ShiftCloseSummary>>;
public sealed record ShiftCloseSummary(Guid ShiftId, decimal Expected, decimal Declared, decimal Variance);

public class CloseShiftHandler : IRequestHandler<CloseShiftCommand, Result<ShiftCloseSummary>>
{
    private readonly IAppDbContext _db;
    private readonly ITenantContext _tenant;
    public CloseShiftHandler(IAppDbContext db, ITenantContext t) { _db = db; _tenant = t; }

    public async Task<Result<ShiftCloseSummary>> Handle(CloseShiftCommand req, CancellationToken ct)
    {
        var shift = await _db.Shifts.FirstOrDefaultAsync(s => s.Id == req.ShiftId, ct);
        if (shift is null) return Error.NotFound("shift.notfound", "Shift not found");
        if (shift.Status != ShiftStatus.Open) return Error.Conflict("shift.closed", "Shift is already closed");

        var cashTotal = await _db.SalePayments.AsNoTracking()
            .Join(_db.Sales.AsNoTracking(), p => p.SaleId, s => s.Id, (p, s) => new { p, s })
            .Where(x => x.s.ShiftId == shift.Id && x.p.Method == "cash" && x.s.Status == SaleStatus.Completed)
            .SumAsync(x => (decimal?)x.p.Amount, ct) ?? 0m;

        shift.ClosingExpected = shift.OpeningFloat + cashTotal;
        shift.ClosingDeclared = req.ClosingDeclared;
        shift.Variance = req.ClosingDeclared - shift.ClosingExpected;
        shift.ClosedByUserId = _tenant.UserId;
        shift.ClosedAt = DateTimeOffset.UtcNow;
        shift.Status = ShiftStatus.Closed;
        await _db.SaveChangesAsync(ct);

        return new ShiftCloseSummary(shift.Id, shift.ClosingExpected, shift.ClosingDeclared, shift.Variance);
    }
}
