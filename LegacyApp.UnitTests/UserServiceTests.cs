using System;
using AutoFixture;
using FluentAssertions;
using LegacyApp.CreditProviders;
using LegacyApp.DataAccess;
using LegacyApp.Models;
using LegacyApp.Repositories;
using LegacyApp.Services;
using LegacyApp.Validators;
using NSubstitute;
using Xunit;

namespace LegacyApp.UnitTests
{
    public class UserServiceTests
    {
        private readonly UserService _sut;
        private readonly IDateTimeProvider _dateTimeProvider = Substitute.For<IDateTimeProvider>();
        private readonly IClientRepository _clientRepository = Substitute.For<IClientRepository>();
        private readonly IUserDataAccess _userDataAccess = Substitute.For<IUserDataAccess>();
        private readonly IUserCreditService _userCreditService = Substitute.For<IUserCreditService>();
        private readonly IFixture _fixture = new Fixture();

        public UserServiceTests()
        {
            _sut = new UserService(_clientRepository, _userDataAccess, new UserValidator(_dateTimeProvider), new CreditLimitProviderFactory(_userCreditService));
        }

        [Fact]
        public void AddUser_ShouldCreateUser_WhenAllParametersAreValid()
        {
            // Arrange
            const int clientId = 1;
            const string firstName = "Nick";
            const string lastName = "Chapsas";
            var dateOfBirth = new DateTime(1993, 1, 1);
            var client = _fixture.Build<Client>()
                .With(c => c.Id, clientId)
                .Create();

            _dateTimeProvider.DateTimeNow.Returns(new DateTime(2021, 2, 16));
            _clientRepository.GetById(clientId).Returns(client);
            _userCreditService.GetCreditLimit(firstName, lastName, dateOfBirth)
                .Returns(600);

            // Act
            var result = _sut.AddUser(firstName, lastName, "nick.chapsas@gmail.com", dateOfBirth, clientId);

            // Assert
            result.Should().BeTrue();
            _userDataAccess.Received(1).AddUser(Arg.Any<User>());
        }

        [Theory]
        [InlineData("", "Chapsas", "nick.chapsas@gmail.com", 1993)]
        [InlineData("Nick", "", "nick.chapsas@gmail.com", 1993)]
        [InlineData("Nick", "Chapsas", "nickcom", 1993)]
        [InlineData("Nick", "Chapsas", "nick.chapsas@gmail.com", 2002)]
        public void AddUser_ShouldNotCreateUser_WhenInputDetailsAreInvalid(
            string firstName, string lastName, string email, int yearOfBirth)
        {
            // Arrange
            const int clientId = 1;
            var dateOfBirth = new DateTime(yearOfBirth, 1, 1);
            var client = _fixture.Build<Client>()
                .With(c => c.Id, () => clientId)
                .Create();

            _dateTimeProvider.DateTimeNow.Returns(new DateTime(2021, 2, 16));
            _clientRepository.GetById(Arg.Is(clientId)).Returns(client);
            _userCreditService.GetCreditLimit(Arg.Is(firstName), Arg.Is(lastName), Arg.Is(dateOfBirth)).Returns(600);

            // Act
            var result = _sut.AddUser(firstName, lastName, email, dateOfBirth, 1);

            // Assert
            result.Should().BeFalse();
        }

        [Theory]
        [InlineData("RandomClientName", true, 600, 600)]
        [InlineData("ImportantClient", true, 600, 1200)]
        [InlineData("VeryImportantClient", false, 0, 0)]
        public void AddUser_ShouldCreateUserWithCorrectCreditLimit_WhenNameIndicatesDifferentClassification(
            string clientName, bool hasCreditLimit, int initialCreditLimit, int finalCreditLimit)
        {
            // Arrange
            const int clientId = 1;
            const string firstName = "Nick";
            const string lastName = "Chapsas";
            var dateOfBirth = new DateTime(1993, 10, 10);
            var client = _fixture.Build<Client>()
                .With(c => c.Id, clientId)
                .With(c => c.Name, clientName)
                .Create();

            _dateTimeProvider.DateTimeNow.Returns(new DateTime(2021, 2, 16));
            _clientRepository.GetById(Arg.Is(clientId)).Returns(client);
            _userCreditService.GetCreditLimit(Arg.Is(firstName), Arg.Is(lastName), Arg.Is(dateOfBirth)).Returns(initialCreditLimit);

            // Act
            var result = _sut.AddUser(firstName, lastName, "nick.chapsas@gmail.com", dateOfBirth, 1);

            // Assert
            result.Should().BeTrue();
            _userDataAccess.Received()
                .AddUser(Arg.Is<User>(user => user.HasCreditLimit == hasCreditLimit && user.CreditLimit == finalCreditLimit));
        }

        [Fact]
        public void AddUser_ShouldNotCreateUser_WhenUserHasCreditLimitAndCreditLimitIsLessThan500()
        {
            // Arrange
            const int clientId = 1;
            const string firstName = "Nick";
            const string lastName = "Chapsas";
            var dateOfBirth = new DateTime(1993, 10, 10);
            var client = _fixture.Build<Client>()
                .With(c => c.Id, () => clientId)
                .Create();

            _dateTimeProvider.DateTimeNow.Returns(new DateTime(2021, 2, 16));
            _clientRepository.GetById(Arg.Is(clientId)).Returns(client);
            _userCreditService.GetCreditLimit(Arg.Is(firstName), Arg.Is(lastName), Arg.Is(dateOfBirth)).Returns(499);

            // Act
            var result = _sut.AddUser(firstName, lastName, "nick.chapsas@gmail.com", dateOfBirth, 1);

            // Assert
            result.Should().BeFalse();
        }
    }
}
