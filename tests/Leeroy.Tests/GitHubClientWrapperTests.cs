using System;
using System.Text;
using System.Threading.Tasks;
using NSubstitute;
using NUnit.Framework;
using NUnit.Framework.Constraints;
using Octokit;

namespace Leeroy.Tests
{
    public class GitHubClientWrapperTests
    {
		[TestFixture]
	    public class TheCreateBlobMethod
	    {
			[Test]
			public void CallsBlobCreate()
			{
				var client = Substitute.For<IGitHubClient>();
				var database = Substitute.For<IGitDatabaseClient>();
				var blob = Substitute.For<IBlobsClient>();
				string content = "a string";
				var returnedBlobReference = new BlobReference();

				client.GitDatabase.Returns(database);
				database.Blob.Returns(blob);
				blob.Create(null, null, null).ReturnsForAnyArgs(Task.FromResult(returnedBlobReference));

				var clientWrapper = new GitHubClientWrapper(client);
				var task = clientWrapper.CreateBlob(c_owner, c_name, content);

				blob.Received().Create(c_owner, c_name, Arg.Is<NewBlob>(x => x.Content == Convert.ToBase64String(Encoding.UTF8.GetBytes(content))));
				Assert.AreEqual(returnedBlobReference, task.Result);
			}
	    }

	    [TestFixture]
	    public class TheCreateBranchMethod
	    {
			[Test]
			public void CallsReferenceCreate()
			{
				var client = Substitute.For<IGitHubClient>();
				var database = Substitute.For<IGitDatabaseClient>();
				var reference = Substitute.For<IReferencesClient>();
				var returnedReference = new Reference();

				client.GitDatabase.Returns(database);
				database.Reference.Returns(reference);
				reference.Create(null, null, null).ReturnsForAnyArgs(Task.FromResult(returnedReference));

				var clientWrapper = new GitHubClientWrapper(client);
				var task = clientWrapper.CreateBranch(c_owner, c_name, c_branch, c_commitSha);

				reference.Received().Create(c_owner, c_name, Arg.Is<NewReference>(x => x.Ref == "refs/heads/" + c_branch && x.Sha == c_commitSha));
				Assert.AreEqual(returnedReference, task.Result);
			}
	    }

		[TestFixture]
		public class TheCreateCommitMethod
		{
			[Test]
			public void CallsCommitCreate()
			{
				var client = Substitute.For<IGitHubClient>();
				var database = Substitute.For<IGitDatabaseClient>();
				var commit = Substitute.For<ICommitsClient>();
				var newCommit = new NewCommit(c_message, c_treeSha);
				var returnedCommit = new Commit();

				client.GitDatabase.Returns(database);
				database.Commit.Returns(commit);
				commit.Create(c_owner, c_name, newCommit).Returns(Task.FromResult(returnedCommit));

				var clientWrapper = new GitHubClientWrapper(client);
				var task = clientWrapper.CreateCommit(c_owner, c_name, newCommit);

				commit.Received().Create(c_owner, c_name, newCommit);
				Assert.AreEqual(returnedCommit, task.Result);
			}
		}

		[TestFixture]
		public class TheCreateTreeMethod
		{
			[Test]
			public void CallsTreeCreate()
			{
				var client = Substitute.For<IGitHubClient>();
				var database = Substitute.For<IGitDatabaseClient>();
				var tree = Substitute.For<ITreesClient>();
				var newTree = new NewTree();
				var returnedTree = new TreeResponse();

				client.GitDatabase.Returns(database);
				database.Tree.Returns(tree);
				tree.Create(c_owner, c_name, newTree).Returns(Task.FromResult(returnedTree));

				var clientWrapper = new GitHubClientWrapper(client);
				var task = clientWrapper.CreateTree(c_owner, c_name, newTree);

				tree.Received().Create(c_owner, c_name, newTree);
				Assert.AreEqual(returnedTree, task.Result);
			}
		}

		[TestFixture]
		public class TheGetBlobMethod
		{
			[Test]
			public void CallsBlobGet()
			{
				var client = Substitute.For<IGitHubClient>();
				var database = Substitute.For<IGitDatabaseClient>();
				var blob = Substitute.For<IBlobsClient>();
				var returnedBlob = new Blob();

				client.GitDatabase.Returns(database);
				database.Blob.Returns(blob);
				blob.Get(c_owner, c_name, c_blobSha).Returns(Task.FromResult(returnedBlob));

				var clientWrapper = new GitHubClientWrapper(client);
				var task = clientWrapper.GetBlob(c_owner, c_name, c_blobSha);

				blob.Received().Get(c_owner, c_name, c_blobSha);
				Assert.AreEqual(returnedBlob, task.Result);
			}
		}

		[TestFixture]
		public class TheGetCommitMethod
		{
			[Test]
			public void CallsCommitGet()
			{
				var client = Substitute.For<IGitHubClient>();
				var database = Substitute.For<IGitDatabaseClient>();
				var commit = Substitute.For<ICommitsClient>();
				var returnedCommit = new Commit();

				client.GitDatabase.Returns(database);
				database.Commit.Returns(commit);
				commit.Get(c_owner, c_name, c_commitSha).Returns(Task.FromResult(returnedCommit));

				var clientWrapper = new GitHubClientWrapper(client);
				var task = clientWrapper.GetCommit(c_owner, c_name, c_commitSha);

				commit.Received().Get(c_owner, c_name, c_commitSha);
				Assert.AreEqual(returnedCommit, task.Result);
			}
		}

		[TestFixture]
		public class TheGetTreeMethod
		{
			[Test]
			public void CallsTreeGet()
			{
				var client = Substitute.For<IGitHubClient>();
				var database = Substitute.For<IGitDatabaseClient>();
				var tree = Substitute.For<ITreesClient>();
				var returnedTree = new TreeResponse();

				client.GitDatabase.Returns(database);
				database.Tree.Returns(tree);
				tree.Get(c_owner, c_name, c_treeSha).Returns(Task.FromResult(returnedTree));

				var clientWrapper = new GitHubClientWrapper(client);
				var task = clientWrapper.GetTree(c_owner, c_name, c_treeSha);

				tree.Received().Get(c_owner, c_name, c_treeSha);
				Assert.AreEqual(returnedTree, task.Result);
			}
		}

		[TestFixture]
		public class TheUpdateBranchMethod
		{
			[Test]
			public void CallsReferenceUpdate()
			{
				var client = Substitute.For<IGitHubClient>();
				var database = Substitute.For<IGitDatabaseClient>();
				var reference = Substitute.For<IReferencesClient>();
				var referenceUpdate = new ReferenceUpdate(c_commitSha);
				var returnedReference = new Reference();
				string referenceName = "refs/heads/" + c_branch;

				client.GitDatabase.Returns(database);
				database.Reference.Returns(reference);
				reference.Update(c_owner, c_name, referenceName, referenceUpdate).Returns(Task.FromResult(returnedReference));

				var clientWrapper = new GitHubClientWrapper(client);
				var task = clientWrapper.UpdateBranch(c_owner, c_name, c_branch, referenceUpdate);

				reference.Received().Update(c_owner, c_name, referenceName, referenceUpdate);
				Assert.AreEqual(returnedReference, task.Result);
			}
		}

		const string c_owner = "owner";
		const string c_name = "name";
		const string c_branch = "branch";
		const string c_blobSha = "0fd0bcfb44f83e7d5ac7a8922578276b9af48746";
		const string c_commitSha = "4015b57a143aec5156fd1444a017a32137a3fd0f";
		const string c_treeSha = "80655da8d80aaaf92ce5357e7828dc09adb00993";
	    const string c_message = "Commit message";
    }
}
