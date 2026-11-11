CREATE DATABASE Insurance_Management_System;
GO

USE Insurance_Management_System;
GO

-- 1. UserAccount
CREATE TABLE UserAccount (
    userId INT IDENTITY(1,1) PRIMARY KEY,
    username VARCHAR(50) NOT NULL UNIQUE,
    email VARCHAR(100) NOT NULL UNIQUE,
    passwordHash VARCHAR(255) NOT NULL,
    role VARCHAR(20) NOT NULL, -- Admin, Agent, Staff, Customer
    status VARCHAR(20) DEFAULT 'Active',
    lastLogin DATETIME2 NULL,
    createdAt DATETIME2 DEFAULT GETDATE()
);

-- 2. Company
CREATE TABLE Company (
    companyId INT IDENTITY(1,1) PRIMARY KEY,
    userId INT NOT NULL UNIQUE FOREIGN KEY REFERENCES UserAccount(userId) ON DELETE CASCADE,
    name NVARCHAR(100) NOT NULL,
    regNo VARCHAR(50) NOT NULL UNIQUE,
    email VARCHAR(100) NOT NULL,
    phone VARCHAR(20) NOT NULL,
    address NVARCHAR(255),
    isHostCompany BIT DEFAULT 0,
    approvalStatus VARCHAR(20) DEFAULT 'Pending',
    commissionRate DECIMAL(5,2) DEFAULT 0.00,
    createdAt DATETIME2 DEFAULT GETDATE()
);

-- 3. Customer
CREATE TABLE Customer (
    customerId INT IDENTITY(1,1) PRIMARY KEY,
    userId INT NOT NULL UNIQUE FOREIGN KEY REFERENCES UserAccount(userId) ON DELETE CASCADE,
    fullName NVARCHAR(100) NOT NULL,
    address NVARCHAR(255),
    phone VARCHAR(20) NOT NULL,
    dateOfBirth DATE,
    nationalId VARCHAR(50)
);

-- 4. Agent
CREATE TABLE Agent (
    agentId INT IDENTITY(1,1) PRIMARY KEY,
    userId INT NOT NULL UNIQUE FOREIGN KEY REFERENCES UserAccount(userId) ON DELETE CASCADE,
    companyId INT NOT NULL FOREIGN KEY REFERENCES Company(companyId),
    fullName NVARCHAR(100) NOT NULL,
    phone VARCHAR(20) NOT NULL,
    branch NVARCHAR(50),
    status VARCHAR(20) DEFAULT 'Active'
);

-- 5. Staff
CREATE TABLE Staff (
    staffId INT IDENTITY(1,1) PRIMARY KEY,
    userId INT NOT NULL UNIQUE FOREIGN KEY REFERENCES UserAccount(userId) ON DELETE CASCADE,
    agentId INT NOT NULL FOREIGN KEY REFERENCES Agent(agentId),
    companyId INT NOT NULL FOREIGN KEY REFERENCES Company(companyId),
    fullName NVARCHAR(100) NOT NULL,
    phone VARCHAR(20),
    receiveAlerts BIT DEFAULT 1,
    status VARCHAR(20) DEFAULT 'Active'
);

-- 6. InsuranceType
CREATE TABLE InsuranceType (
    inTypeId INT IDENTITY(1,1) PRIMARY KEY,
    name NVARCHAR(50) NOT NULL UNIQUE,
    description NVARCHAR(255)
);

-- 7. InsuranceProduct
CREATE TABLE InsuranceProduct (
    inProId INT IDENTITY(1,1) PRIMARY KEY,
    companyId INT NOT NULL FOREIGN KEY REFERENCES Company(companyId),
    inTypeId INT NOT NULL FOREIGN KEY REFERENCES InsuranceType(inTypeId),
    name NVARCHAR(100) NOT NULL,
    basePremium DECIMAL(12,2) NOT NULL,
    detail NVARCHAR(255),
    status VARCHAR(20) DEFAULT 'Active'
);

-- 8. Quotation
CREATE TABLE Quotation (
    quoteId INT IDENTITY(1,1) PRIMARY KEY,
    customerId INT NOT NULL FOREIGN KEY REFERENCES Customer(customerId),
    inProId INT NOT NULL FOREIGN KEY REFERENCES InsuranceProduct(inProId),
    agentId INT NULL FOREIGN KEY REFERENCES Agent(agentId) ON DELETE SET NULL,
    requestDate DATETIME2 DEFAULT GETDATE(),
    message NVARCHAR(255),
    estimatedPremium DECIMAL(12,2) NOT NULL,
    status VARCHAR(20) DEFAULT 'Pending'
);

-- 9. InsurancePolicy
CREATE TABLE InsurancePolicy (
    inPoId INT IDENTITY(1,1) PRIMARY KEY,
    quoteId INT NOT NULL UNIQUE FOREIGN KEY REFERENCES Quotation(quoteId),
    agentId INT NULL FOREIGN KEY REFERENCES Agent(agentId) ON DELETE SET NULL,
    policyNumber VARCHAR(50) NOT NULL UNIQUE,
    startDate DATETIME2 NOT NULL,
    endDate DATETIME2 NOT NULL,
    coverageAmount DECIMAL(12,2) NOT NULL,
    premiumAmount DECIMAL(12,2) NOT NULL,
    status VARCHAR(20) DEFAULT 'Active'
);

-- 10. Claim
CREATE TABLE Claim (
    claimId INT IDENTITY(1,1) PRIMARY KEY,
    inPoId INT NOT NULL FOREIGN KEY REFERENCES InsurancePolicy(inPoId),
    claimDate DATETIME2 DEFAULT GETDATE(),
    incidentDate DATETIME2 NOT NULL,
    incidentType NVARCHAR(50) NOT NULL,
    incidentLocation NVARCHAR(100) NOT NULL,
    claimAmount DECIMAL(12,2) NOT NULL,
    status VARCHAR(20) DEFAULT 'Submitted'
);

-- 11. Payment
CREATE TABLE Payment (
    payId INT IDENTITY(1,1) PRIMARY KEY,
    inPoId INT NOT NULL FOREIGN KEY REFERENCES InsurancePolicy(inPoId),
    transactionRef VARCHAR(100) NOT NULL UNIQUE,
    payDate DATETIME2 DEFAULT GETDATE(),
    payAmount DECIMAL(12,2) NOT NULL,
    payMethod VARCHAR(50) NOT NULL,
    payStatus VARCHAR(20) DEFAULT 'Completed'
);

-- 12. Beneficiary
CREATE TABLE Beneficiary (
    benId INT IDENTITY(1,1) PRIMARY KEY,
    inPoId INT NOT NULL FOREIGN KEY REFERENCES InsurancePolicy(inPoId) ON DELETE CASCADE,
    name NVARCHAR(100) NOT NULL,
    relation NVARCHAR(50) NOT NULL,
    phone VARCHAR(20),
    sharePercentage DECIMAL(5,2) DEFAULT 100.00
);

-- 13. Notification
CREATE TABLE Notification (
    notificationId INT IDENTITY(1,1) PRIMARY KEY,
    userId INT NULL FOREIGN KEY REFERENCES UserAccount(userId) ON DELETE CASCADE,
    companyId INT NULL FOREIGN KEY REFERENCES Company(companyId),
    targetRole VARCHAR(20) NULL,
    title NVARCHAR(100) NOT NULL,
    message NVARCHAR(500) NOT NULL,
    linkUrl NVARCHAR(255),
    isRead BIT DEFAULT 0,
    createdAt DATETIME2 DEFAULT GETDATE(),
    type VARCHAR(50) DEFAULT 'General'
);
GO