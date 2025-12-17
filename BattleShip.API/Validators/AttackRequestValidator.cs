using FluentValidation;
using BattleShip.Models;

namespace BattleShip.API.Validators;

public class AttackRequestValidator : AbstractValidator<AttackRequest>
{
    public AttackRequestValidator()
    {
        RuleFor(x => x.X)
            .InclusiveBetween(0, Board.Size - 1)
            .WithMessage($"X must be between 0 and {Board.Size - 1}");

        RuleFor(x => x.Y)
            .InclusiveBetween(0, Board.Size - 1)
            .WithMessage($"Y must be between 0 and {Board.Size - 1}");
    }
}